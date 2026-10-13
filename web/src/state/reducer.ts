import {
  isSupportedUpdate,
  type ConnectionStatus,
  type IntentDto,
  type IntentResult,
  type TableUpdate,
  type TableView,
} from "../protocol/types";

export type Session = "idle" | "starting" | "playing" | "finished" | "lost";

export type LostReason = "dropped" | "replaced" | "unknown";

export type Notice =
  | { kind: "rejection"; code: string }
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
  | { type: "resync/handled" };

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
      const next: AppState = { ...state, connection: action.status };
      // A dropped connection can no longer own the table, so a game that was in progress is gone.
      if (
        (action.status === "reconnecting" || action.status === "unavailable") &&
        (state.session === "playing" || state.session === "starting")
      ) {
        return { ...lose(next, "dropped") };
      }
      return next;
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
        game: update.view,
        lastVersion: update.version,
        session: update.finished ? "finished" : "playing",
        lostReason: null,
        pending,
        needsResync: gap ? true : state.needsResync,
        // A newer accepted state supersedes any earlier refusal message.
        notice: state.notice?.kind === "rejection" ? null : state.notice,
      };
    }

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
