import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef, type ReactNode } from "react";
import type { HubClient } from "../connection/hubClient";
import { useLanguage } from "../i18n/LanguageProvider";
import { unrotateIntent } from "../protocol/perspective";
import type { IntentDto, RoomResult, RoomState, Seat } from "../protocol/types";
import { clearSession, getIdentityToken, loadName, saveName, saveSession } from "../session/storage";
import { navigate, roomPath } from "../routing/router";
import { setLabels } from "../components/labels";
import { initialState, reducer, type AppState } from "./reducer";
import { canAct, sameIntent, seatLabels } from "./selectors";

interface StoreValue {
  state: AppState;
  /** Legacy single-table start (no room), or "play again" when a room is open. */
  startGame: () => void;
  submit: (intent: IntentDto) => void;
  dismissNotice: () => void;

  // Rooms
  /** Points the store at a room (from the URL or "return to your game") and joins/rejoins it. */
  enterRoom: (roomId: string | null) => void;
  /** Creates a room for this person and opens it; resolves to the new room id (or null on failure). */
  createRoom: (name: string) => Promise<string | null>;
  /** Joins the targeted room with a name the person just chose. */
  joinWithName: (name: string) => Promise<boolean>;
  claimSeat: (seat: Seat) => Promise<void>;
  releaseSeat: () => Promise<void>;
  startRoomGame: () => Promise<void>;
  skipWait: () => Promise<void>;
  leaveRoom: () => Promise<void>;
  setName: (name: string) => Promise<boolean>;
  takeBack: () => Promise<void>;
  dismissRecap: () => void;
}

const StoreContext = createContext<StoreValue | null>(null);

export function StoreProvider({ client, children }: { client: HubClient; children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, initialState);
  const { t } = useLanguage();
  const stateRef = useRef(state);
  stateRef.current = state;
  // Bumped whenever the table's state may have changed; a "no table" answer to an older read is stale.
  const epoch = useRef(0);
  const targetRoom = useRef<string | null>(null);

  // Display names follow the room state; the labels are plain data derived from it.
  const labels = useMemo(() => seatLabels(state.room, t), [state.room, t]);
  setLabels(labels.seats, labels.teams);

  const sync = useCallback(async () => {
    const askedAt = epoch.current;
    try {
      const result = await client.getView();
      if (result.update) {
        epoch.current += 1;
        dispatch({ type: "view/received", update: result.update });
      } else if (result.rejection?.category === "notSeated" && epoch.current === askedAt) {
        dispatch({ type: "sync/noTable" });
      }
    } catch {
      // Not connected right now; the next connection triggers another sync.
    }
  }, [client]);

  /** Applies the outcome of any room call. Returns true when it succeeded. */
  const apply = useCallback((result: RoomResult, joining = false): boolean => {
    if (result.ok) {
      const state: RoomState | null | undefined = result.state;
      if (state) {
        dispatch({ type: "room/received", state, nowMs: Date.now() });
        saveSession(state.roomId);
      }
      if (result.update) {
        epoch.current += 1;
        dispatch({ type: "view/reset", update: result.update });
      }
      if (result.awayMoves !== null && result.awayMoves !== undefined) {
        dispatch({ type: "recap/set", moves: result.awayMoves });
      }
      return true;
    }
    const code = result.rejection?.code ?? "unknown";
    switch (code) {
      case "RoomNotFound":
        clearSession();
        dispatch({ type: "room/error", kind: "notFound" });
        break;
      case "RoomEnded":
        clearSession();
        dispatch({ type: "room/error", kind: "ended" });
        break;
      case "RoomFull":
        dispatch({ type: "room/error", kind: "full" });
        break;
      case "Superseded":
        dispatch({ type: "room/superseded" });
        break;
      case "NameInvalid":
      case "NameTaken":
        if (joining) dispatch({ type: "room/needsName", code });
        else dispatch({ type: "room/rejected", code });
        break;
      default:
        dispatch({ type: "room/rejected", code });
    }
    return false;
  }, []);

  /** Brings this connection into the targeted room: rejoin first, then join with the saved name. */
  const attach = useCallback(async () => {
    const roomId = targetRoom.current;
    if (!roomId) {
      await sync();
      return;
    }
    const token = getIdentityToken();
    try {
      const rejoined = await client.rejoin({ roomId, token });
      if (rejoined.ok || rejoined.rejection?.code !== "NotInRoom") {
        apply(rejoined, true);
        return;
      }
      const name = loadName();
      if (!name) {
        dispatch({ type: "room/needsName", code: null });
        return;
      }
      apply(await client.joinRoom({ roomId, token, name }), true);
    } catch {
      // Not connected right now; the next connection triggers another attempt.
    }
  }, [apply, client, sync]);

  useEffect(() => {
    client.start({
      status: (status) => {
        dispatch({ type: "connection/status", status });
        // Always re-read the server's view before trusting anything shown on the table.
        if (status === "connected" && !stateRef.current.superseded) void attach();
      },
      update: (update) => {
        epoch.current += 1;
        dispatch({ type: "view/received", update });
      },
      tableClosed: (message) => {
        epoch.current += 1;
        if (message.reason === "ended") {
          clearSession();
          dispatch({ type: "room/error", kind: "ended" });
        } else if (message.reason === "left") {
          dispatch({ type: "room/left" });
        } else {
          dispatch({ type: "table/closed" });
        }
      },
      room: (room) => dispatch({ type: "room/received", state: room, nowMs: Date.now() }),
      superseded: () => dispatch({ type: "room/superseded" }),
    });
    return () => {
      void client.stop();
    };
  }, [client, attach]);

  useEffect(() => {
    const nudge = () => {
      const current = stateRef.current;
      if (document.visibilityState === "visible" && current.connection === "connected" && !current.superseded) {
        void attach();
      }
    };
    document.addEventListener("visibilitychange", nudge);
    return () => document.removeEventListener("visibilitychange", nudge);
  }, [attach]);

  useEffect(() => {
    if (state.needsResync) {
      dispatch({ type: "resync/handled" });
      void sync();
    }
  }, [state.needsResync, sync]);

  const enterRoom = useCallback(
    (roomId: string | null) => {
      if (targetRoom.current === roomId) return;
      targetRoom.current = roomId;
      if (roomId === null) return;
      const current = stateRef.current;
      // Already inside that room (e.g. just created it): nothing to do.
      if (current.room?.roomId === roomId) return;
      if (current.connection === "connected") void attach();
    },
    [attach],
  );

  const createRoom = useCallback(
    async (name: string): Promise<string | null> => {
      const current = stateRef.current;
      if (current.connection !== "connected") return null;
      try {
        const result = await client.createRoom({ token: getIdentityToken(), name });
        if (!apply(result, true) || !result.roomId) return null;
        saveName(name);
        targetRoom.current = result.roomId;
        navigate(roomPath(result.roomId));
        return result.roomId;
      } catch {
        dispatch({ type: "room/rejected", code: "unknown" });
        return null;
      }
    },
    [apply, client],
  );

  const joinWithName = useCallback(
    async (name: string): Promise<boolean> => {
      const roomId = targetRoom.current;
      if (!roomId) return false;
      try {
        const result = await client.joinRoom({ roomId, token: getIdentityToken(), name });
        const ok = apply(result, true);
        if (ok) saveName(name);
        return ok;
      } catch {
        return false;
      }
    },
    [apply, client],
  );

  const call = useCallback(
    async (invoke: () => Promise<RoomResult>): Promise<boolean> => {
      try {
        return apply(await invoke());
      } catch {
        dispatch({ type: "room/rejected", code: "unknown" });
        return false;
      }
    },
    [apply],
  );

  const startGame = useCallback(() => {
    const current = stateRef.current;
    if (current.connection !== "connected" || current.session === "starting") return;
    if (current.room) {
      void call(() => client.playAgain());
      return;
    }
    if (current.session === "playing") return;
    epoch.current += 1;
    dispatch({ type: "start/begin" });
    void (async () => {
      try {
        const created = await client.createTable();
        if (!created.ok) {
          dispatch({ type: "start/failed", code: created.rejection?.code });
          await sync();
          return;
        }
        const seated = await client.takeSeat();
        if (!seated.ok) {
          dispatch({ type: "start/failed", code: seated.rejection?.code });
          return;
        }
        // The first update normally arrives as a push; reading the view makes the start robust to ordering.
        await sync();
      } catch {
        dispatch({ type: "start/failed" });
      }
    })();
  }, [call, client, sync]);

  const submit = useCallback(
    (intent: IntentDto) => {
      const current = stateRef.current;
      if (!canAct(current)) return;
      // Only an echo of something the server currently lists as legal may be sent.
      if (!current.game?.legalActions.some((a) => sameIntent(a, intent))) return;
      dispatch({ type: "intent/sent", intent });
      void (async () => {
        try {
          const result = await client.submitIntent(unrotateIntent(intent, current.viewer));
          dispatch({ type: "intent/result", result });
        } catch {
          dispatch({ type: "intent/failed" });
        }
      })();
    },
    [client],
  );

  const dismissNotice = useCallback(() => dispatch({ type: "notice/dismiss" }), []);
  const dismissRecap = useCallback(() => dispatch({ type: "recap/set", moves: null }), []);

  const claimSeat = useCallback(
    async (seat: Seat) => {
      await call(() => client.claimSeat(seat));
    },
    [call, client],
  );
  const releaseSeat = useCallback(async () => {
    await call(() => client.releaseSeat());
  }, [call, client]);
  const startRoomGame = useCallback(async () => {
    await call(() => client.startRoomGame());
  }, [call, client]);
  const skipWait = useCallback(async () => {
    await call(() => client.skipWait());
  }, [call, client]);
  const takeBack = useCallback(async () => {
    const ok = await call(() => client.takeBack());
    if (ok) {
      dispatch({ type: "room/received", state: stateRef.current.room!, nowMs: Date.now() });
      // A fresh view comes with the next push; ask for it now so the table is not stale.
      void sync();
    }
  }, [call, client, sync]);
  const setName = useCallback(
    async (name: string) => {
      const ok = await call(() => client.setName(name));
      if (ok) saveName(name.trim());
      return ok;
    },
    [call, client],
  );
  const leaveRoom = useCallback(async () => {
    try {
      await client.leaveRoom();
    } catch {
      // The connection may be gone; leaving locally is still what the person asked for.
    }
    clearSession();
    targetRoom.current = null;
    dispatch({ type: "room/left" });
    navigate("/");
  }, [client]);

  const value = useMemo<StoreValue>(
    () => ({
      state,
      startGame,
      submit,
      dismissNotice,
      enterRoom,
      createRoom,
      joinWithName,
      claimSeat,
      releaseSeat,
      startRoomGame,
      skipWait,
      leaveRoom,
      setName,
      takeBack,
      dismissRecap,
    }),
    [
      state,
      startGame,
      submit,
      dismissNotice,
      enterRoom,
      createRoom,
      joinWithName,
      claimSeat,
      releaseSeat,
      startRoomGame,
      skipWait,
      leaveRoom,
      setName,
      takeBack,
      dismissRecap,
    ],
  );
  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>;
}

export function useStore(): StoreValue {
  const value = useContext(StoreContext);
  if (!value) throw new Error("useStore must be used inside StoreProvider");
  return value;
}
