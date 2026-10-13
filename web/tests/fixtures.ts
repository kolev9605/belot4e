import type { HubClient, HubHandlers } from "../src/connection/hubClient";
import type {
  ConnectionStatus,
  Seat,
  CreateTableResult,
  GetViewResult,
  HandSummary,
  IntentDto,
  IntentResult,
  RoomResult,
  RoomState,
  TableUpdate,
  TableView,
  TakeSeatResult,
  TeamHand,
} from "../src/protocol/types";

export function makeView(overrides: Partial<TableView> = {}): TableView {
  return {
    seat: "south",
    phase: "playing",
    dealer: "west",
    toAct: "south",
    dealNumber: 1,
    ownHand: ["AS", "10S", "QH", "7H", "8C"],
    handSizes: { north: 5, east: 5, south: 5, west: 5 },
    biddingHistory: [],
    standingBid: null,
    standingBidder: null,
    contra: false,
    recontra: false,
    contract: { kind: "hearts", declarer: "east", multiplier: 1 },
    leader: "south",
    currentTrick: [],
    lastCompletedTrick: null,
    completedTricks: [],
    announcements: [],
    ownDeclared: [],
    belotes: [],
    matchScore: { northSouth: 34, eastWest: 51 },
    heldOverPoints: 0,
    winner: null,
    lastHand: null,
    legalActions: [
      { kind: "play", seat: "south", card: "QH" },
      { kind: "play", seat: "south", card: "7H" },
    ],
    ...overrides,
  };
}

export function makeUpdate(version: number, view: Partial<TableView> = {}, finished = false): TableUpdate {
  return { protocolVersion: 2, version, finished, view: makeView(view) };
}

export const ROOM_ID = "3f2b8c1e-9a4d-4e7b-8c55-0d1f2a3b4c5d";

type SeatSpec = Partial<RoomState["seats"][number]> & { seat: Seat };

/** A room as pushed by the server: Ivo (host) at south and Mira at north, bots elsewhere unless overridden. */
export function makeRoomState(overrides: Partial<RoomState> = {}, seats: SeatSpec[] = []): RoomState {
  const base: RoomState["seats"] = [
    { seat: "north", kind: "human", participantId: "p-2", name: "Mira", connection: "connected", deadlineUtc: null, botFor: null, origin: null },
    { seat: "east", kind: "bot", participantId: null, name: "Bot", connection: null, deadlineUtc: null, botFor: null, origin: "default" },
    { seat: "south", kind: "human", participantId: "p-1", name: "Ivo", connection: "connected", deadlineUtc: null, botFor: null, origin: null },
    { seat: "west", kind: "bot", participantId: null, name: "Bot", connection: null, deadlineUtc: null, botFor: null, origin: "default" },
  ];
  const merged = base.map((b) => ({ ...b, ...(seats.find((s) => s.seat === b.seat) ?? {}) }));
  return {
    protocolVersion: 2,
    roomId: ROOM_ID,
    revision: 1,
    serverNowUtc: new Date().toISOString(),
    lifecycle: "lobby",
    hostId: "p-1",
    you: { participantId: "p-1", seat: "south", isHost: true },
    seats: merged,
    missing: [],
    pendingTakeovers: [],
    lastEvent: null,
    ...overrides,
  };
}

/** A hub client driven by the test: it records calls and lets the test push events. */
export class FakeHubClient implements HubClient {
  handlers: HubHandlers | null = null;
  calls: string[] = [];
  submitted: IntentDto[] = [];
  createResult: CreateTableResult = { ok: true, tableId: "t1" };
  seatResult: TakeSeatResult = { ok: true, version: 0 };
  viewResult: GetViewResult = { rejection: { category: "notSeated", code: "NotSeated" } };
  intentResult: IntentResult | (() => Promise<IntentResult>) = { accepted: true, version: 1 };

  start(handlers: HubHandlers): void {
    this.handlers = handlers;
    this.calls.push("start");
  }
  async stop(): Promise<void> {
    this.calls.push("stop");
  }
  async createTable(): Promise<CreateTableResult> {
    this.calls.push("createTable");
    return this.createResult;
  }
  async takeSeat(): Promise<TakeSeatResult> {
    this.calls.push("takeSeat");
    return this.seatResult;
  }
  async submitIntent(intent: IntentDto): Promise<IntentResult> {
    this.calls.push("submitIntent");
    this.submitted.push(intent);
    return typeof this.intentResult === "function" ? this.intentResult() : this.intentResult;
  }
  async getView(): Promise<GetViewResult> {
    this.calls.push("getView");
    return this.viewResult;
  }

  // ---- rooms -------------------------------------------------------------------------------
  roomResult: RoomResult = { ok: false, rejection: { category: "room", code: "RoomNotFound" } };
  rejoinResult: RoomResult | null = null;
  joined: { roomId: string; token: string; name: string }[] = [];
  created: { token: string; name: string }[] = [];
  roomCalls: string[] = [];

  private record(name: string, result?: RoomResult): Promise<RoomResult> {
    this.calls.push(name);
    this.roomCalls.push(name);
    return Promise.resolve(result ?? this.roomResult);
  }
  async createRoom(request: { token: string; name: string }): Promise<RoomResult> {
    this.created.push({ token: request.token, name: request.name });
    return this.record("createRoom");
  }
  async joinRoom(request: { roomId: string; token: string; name: string }): Promise<RoomResult> {
    this.joined.push(request);
    return this.record("joinRoom");
  }
  async rejoin(): Promise<RoomResult> {
    return this.record("rejoin", this.rejoinResult ?? undefined);
  }
  async setName(): Promise<RoomResult> {
    return this.record("setName");
  }
  async claimSeat(): Promise<RoomResult> {
    return this.record("claimSeat");
  }
  async releaseSeat(): Promise<RoomResult> {
    return this.record("releaseSeat");
  }
  async startRoomGame(): Promise<RoomResult> {
    return this.record("startRoomGame");
  }
  async skipWait(): Promise<RoomResult> {
    return this.record("skipWait");
  }
  async leaveRoom(): Promise<RoomResult> {
    return this.record("leaveRoom", { ok: true });
  }
  async playAgain(): Promise<RoomResult> {
    return this.record("playAgain");
  }
  async takeBack(): Promise<RoomResult> {
    return this.record("takeBack");
  }

  status(status: ConnectionStatus) {
    this.handlers?.status(status);
  }
  push(update: TableUpdate) {
    this.handlers?.update(update);
  }
  close() {
    this.handlers?.tableClosed({ protocolVersion: 2, reason: "replaced" });
  }
  pushRoom(state: RoomState) {
    this.handlers?.room(state);
  }
  supersede() {
    this.handlers?.superseded();
  }
}

export function makeSummary(overrides: Partial<HandSummary> = {}): HandSummary {
  const team = (cards: string[], extra: Partial<TeamHand> = {}): TeamHand => ({
    wonCards: cards,
    cardPoints: 60,
    lastTrickBonus: 0,
    announcementPoints: 0,
    belotePoints: 0,
    allTricksBonus: 0,
    rawTotal: 60,
    gamePoints: 6,
    ...extra,
  });
  return {
    dealNumber: 1,
    contract: { kind: "hearts", declarer: "east", multiplier: 1 },
    declaringTeam: "eastWest",
    outcome: "made",
    teams: {
      northSouth: team(["AS", "10S", "KS", "QS", "JH", "9H", "8H", "7H"], { cardPoints: 52, rawTotal: 52, gamePoints: 5 }),
      eastWest: team(
        ["AH", "10H", "KH", "QH", "AD", "10D", "KD", "QD", "AC", "10C", "KC", "QC", "JS", "9S", "8S", "7S", "JD", "9D", "8D", "7D", "JC", "9C", "8C", "7C"],
        { cardPoints: 100, lastTrickBonus: 10, announcementPoints: 20, belotePoints: 20, rawTotal: 150, gamePoints: 15 },
      ),
    },
    matchScore: { northSouth: 39, eastWest: 66 },
    heldOver: { before: 0, awarded: 0, awardedTo: null, newlyHeld: 0, after: 0 },
    winner: null,
    ...overrides,
  };
}
