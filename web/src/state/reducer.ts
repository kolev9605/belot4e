import { rotateView } from "../protocol/perspective";
import {
  isSupportedUpdate,
  type ConnectionStatus,
  type IntentDto,
  type IntentResult,
  type RoomState,
  type Seat,
  type TableUpdate,
  type TableView,
} from "../protocol/types";

export type Session = "idle" | "starting" | "playing" | "finished" | "lost";

export type LostReason = "dropped" | "replaced" | "unknown";

export type RoomErrorKind = "notFound" | "ended" | "full";

export type Notice =
  | { kind: "rejection"; code: string }
  | { kind: "roomRejection"; code: string }
  | { kind: "versionMismatch" }
  | { kind: "startFailed"; code?: string }
  | { kind: "error" };

export interface PendingAction {
  intent: IntentDto;
  /** Set once the server accepted the intent; cleared when that version has been applied. */
  awaitingVersion: number | null;
}

export interface AppState {
  connection: ConnectionStatus;
  session: Session;
  lostReason: LostReason | null;
  game: TableView | null;
  lastVersion: number | null;
  pending: PendingAction | null;
  notice: Notice | null;
  /** Set when a version gap was seen; the store answers with a GetView and clears it. */
  needsResync: boolean;
  /** The protocol version is unsupported; input stays disabled. */
  blocked: boolean;

  // ---- rooms -------------------------------------------------------------------------------
  /** The seat the server knows this person by (the shown view is rotated so this is "south"). */
  viewer: Seat | null;
  room: RoomState | null;
  /** serverNow - clientNow at the last room push, for countdowns that survive clock skew. */
  clockOffsetMs: number;
  /** Another tab took this person over; this tab must not rejoin on its own. */
  superseded: boolean;
  roomError: RoomErrorKind | null;
  /** A name is needed (none saved, or the saved one was refused) before joining. */
  needsName: { code: string | null } | null;
  /** Moves a bot made for this person while they were away; shown once. */
  awayRecap: number | null;
}

export const initialState: AppState = {
  connection: "connecting",
  session: "idle",
  lostReason: null,
  game: null,
  lastVersion: null,
  pending: null,
  notice: null,
  needsResync: false,
  blocked: false,
  viewer: null,
  room: null,
  clockOffsetMs: 0,
  superseded: false,
  roomError: null,
  needsName: null,
  awayRecap: null,
};

export type Action =
  | { type: "connection/status"; status: ConnectionStatus }
  | { type: "view/received"; update: TableUpdate }
  | { type: "sync/noTable" }
  | { type: "table/closed" }
  | { type: "start/begin" }
  | { type: "start/failed"; code?: string }
  | { type: "intent/sent"; intent: IntentDto }
  | { type: "intent/result"; result: IntentResult }
  | { type: "intent/failed" }
  | { type: "notice/dismiss" }
  | { type: "resync/handled" }
  | { type: "room/received"; state: RoomState; nowMs: number }
  | { type: "view/reset"; update: TableUpdate }
  | { type: "room/superseded" }
  | { type: "room/error"; kind: RoomErrorKind }
  | { type: "room/needsName"; code: string | null }
  | { type: "room/left" }
  | { type: "room/rejected"; code: string }
  | { type: "recap/set"; moves: number | null };

function lose(state: AppState, reason: LostReason): AppState {
  return {
    ...state,
    session: "lost",
    lostReason: reason,
    game: null,
    lastVersion: null,
    pending: null,
    needsResync: false,
  };
}

export function reducer(state: AppState, action: Action): AppState {
  switch (action.type) {
    case "connection/status": {
      // The table stays on screen (marked stale) while the connection comes back; the store then
      // rejoins the room or re-reads the view before anything is trusted again.
      return { ...state, connection: action.status };
    }

    case "view/received": {
      const { update } = action;
      if (!isSupportedUpdate(update)) {
        return { ...state, blocked: true, notice: { kind: "versionMismatch" } };
      }
      if (state.session === "lost") return state;
      if (state.lastVersion !== null && update.version <= state.lastVersion) return state;

      const gap = state.lastVersion !== null && update.version > state.lastVersion + 1;
      const pending =
        state.pending && state.pending.awaitingVersion !== null && update.version >= state.pending.awaitingVersion
          ? null
          : state.pending;
      return {
        ...state,
        game: rotateView(update.view),
        viewer: update.view.seat,
        lastVersion: update.version,
        session: update.finished ? "finished" : "playing",
        lostReason: null,
        pending,
        needsResync: gap ? true : state.needsResync,
        // A newer accepted state supersedes any earlier refusal message.
        notice: state.notice?.kind === "rejection" ? null : state.notice,
      };
    }

    case "view/reset": {
      const { update } = action;
      if (!isSupportedUpdate(update)) {
        return { ...state, blocked: true, notice: { kind: "versionMismatch" } };
      }
      return {
        ...state,
        game: rotateView(update.view),
        viewer: update.view.seat,
        lastVersion: update.version,
        session: update.finished ? "finished" : "playing",
        lostReason: null,
        pending: null,
        needsResync: false,
        superseded: false,
      };
    }

    case "room/received": {
      const incoming = action.state;
      if (state.room && state.room.roomId === incoming.roomId && incoming.revision < state.room.revision) {
        return state;
      }
      return {
        ...state,
        room: incoming,
        clockOffsetMs: Date.parse(incoming.serverNowUtc) - action.nowMs,
        roomError: null,
        needsName: null,
      };
    }

    case "room/superseded":
      return { ...state, superseded: true };

    case "room/error":
      return { ...state, room: null, game: null, lastVersion: null, session: "idle", roomError: action.kind, needsName: null };

    case "room/needsName":
      return { ...state, needsName: { code: action.code }, roomError: null };

    case "room/left":
      return {
        ...state,
        room: null,
        game: null,
        viewer: null,
        lastVersion: null,
        session: "idle",
        pending: null,
        notice: null,
        superseded: false,
        roomError: null,
        needsName: null,
        awayRecap: null,
      };

    case "room/rejected":
      return { ...state, notice: { kind: "roomRejection", code: action.code } };

    case "recap/set":
      return { ...state, awayRecap: action.moves };

    case "sync/noTable": {
      // While starting, the table may simply not exist yet; only an active game can be lost this way.
      if (state.session === "playing") return lose(state, "dropped");
      return { ...state, needsResync: false };
    }

    case "table/closed":
      return lose(state, "replaced");

    case "start/begin":
      return {
        ...state,
        session: "starting",
        lostReason: null,
        game: null,
        lastVersion: null,
        pending: null,
        notice: null,
        needsResync: false,
      };

    case "start/failed":
      return {
        ...state,
        session: "idle",
        notice: { kind: "startFailed", code: action.code },
      };

    case "intent/sent":
      return { ...state, pending: { intent: action.intent, awaitingVersion: null } };

    case "intent/result": {
      const { result } = action;
      if (!result.accepted) {
        const code = result.rejection?.code ?? "unknown";
        const category = result.rejection?.category;
        if (category === "notSeated") return lose(state, "dropped");
        return { ...state, pending: null, notice: { kind: "rejection", code } };
      }
      const version = result.version ?? null;
      const applied = version !== null && state.lastVersion !== null && state.lastVersion >= version;
      return {
        ...state,
        pending: applied || version === null ? null : state.pending && { ...state.pending, awaitingVersion: version },
        notice: null,
      };
    }

    case "intent/failed":
      return { ...state, pending: null, notice: { kind: "error" } };

    case "notice/dismiss":
      return { ...state, notice: null };

    case "resync/handled":
      return { ...state, needsResync: false };
  }
}
