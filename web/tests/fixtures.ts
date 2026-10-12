import type { HubClient, HubHandlers } from "../src/connection/hubClient";
import type {
  ConnectionStatus,
  CreateTableResult,
  GetViewResult,
  IntentDto,
  IntentResult,
  TableUpdate,
  TableView,
  TakeSeatResult,
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
