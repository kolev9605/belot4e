import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef, type ReactNode } from "react";
import type { HubClient } from "../connection/hubClient";
import type { IntentDto } from "../protocol/types";
import { initialState, reducer, type AppState } from "./reducer";
import { canAct, sameIntent } from "./selectors";

interface StoreValue {
  state: AppState;
  startGame: () => void;
  submit: (intent: IntentDto) => void;
  dismissNotice: () => void;
}

const StoreContext = createContext<StoreValue | null>(null);

export function StoreProvider({ client, children }: { client: HubClient; children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, initialState);
  const stateRef = useRef(state);
  stateRef.current = state;
  // Bumped whenever the table's state may have changed; a "no table" answer to an older read is stale.
  const epoch = useRef(0);

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

  useEffect(() => {
    client.start({
      status: (status) => {
        dispatch({ type: "connection/status", status });
        // Always re-read the server's view before trusting anything shown on the table.
        if (status === "connected") void sync();
      },
      update: (update) => {
        epoch.current += 1;
        dispatch({ type: "view/received", update });
      },
      tableClosed: () => {
        epoch.current += 1;
        dispatch({ type: "table/closed" });
      },
    });
    return () => {
      void client.stop();
    };
  }, [client, sync]);

  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState === "visible" && stateRef.current.connection === "connected") void sync();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => document.removeEventListener("visibilitychange", onVisible);
  }, [sync]);

  useEffect(() => {
    if (state.needsResync) {
      dispatch({ type: "resync/handled" });
      void sync();
    }
  }, [state.needsResync, sync]);

  const startGame = useCallback(() => {
    const current = stateRef.current;
    if (current.connection !== "connected" || current.session === "starting" || current.session === "playing") return;
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
  }, [client, sync]);

  const submit = useCallback(
    (intent: IntentDto) => {
      const current = stateRef.current;
      if (!canAct(current)) return;
      // Only an echo of something the server currently lists as legal may be sent.
      if (!current.game?.legalActions.some((a) => sameIntent(a, intent))) return;
      dispatch({ type: "intent/sent", intent });
      void (async () => {
        try {
          const result = await client.submitIntent(intent);
          dispatch({ type: "intent/result", result });
        } catch {
          dispatch({ type: "intent/failed" });
        }
      })();
    },
    [client],
  );

  const dismissNotice = useCallback(() => dispatch({ type: "notice/dismiss" }), []);

  const value = useMemo(
    () => ({ state, startGame, submit, dismissNotice }),
    [state, startGame, submit, dismissNotice],
  );
  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>;
}

export function useStore(): StoreValue {
  const value = useContext(StoreContext);
  if (!value) throw new Error("useStore must be used inside StoreProvider");
  return value;
}
