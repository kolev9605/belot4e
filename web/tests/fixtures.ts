import type { HubClient, HubHandlers } from "../src/connection/hubClient";
import type {
  ConnectionStatus,
  CreateTableResult,
  GetViewResult,
  HandSummary,
  IntentDto,
  IntentResult,
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
  return { protocolVersion: 1, version, finished, view: makeView(view) };
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

  status(status: ConnectionStatus) {
    this.handlers?.status(status);
  }
  push(update: TableUpdate) {
    this.handlers?.update(update);
  }
  close() {
    this.handlers?.tableClosed({ protocolVersion: 1, reason: "replaced" });
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
