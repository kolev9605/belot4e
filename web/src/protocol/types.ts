// Wire types mirroring specs/003-playable-table-ui/contracts/hub-client.md
// and specs/002-live-table-vs-bots/contracts/hub-protocol.md. Protocol version 1.

export const PROTOCOL_VERSION = 1;
export const CLIENT_SEAT = "south";

export type Seat = "north" | "east" | "south" | "west";
export type Team = "northSouth" | "eastWest";
export type Phase = "bidding" | "playing" | "finished";
export type ContractKind = "clubs" | "diamonds" | "hearts" | "spades" | "noTrump" | "allTrump";
export type SuitName = "clubs" | "diamonds" | "hearts" | "spades";
/** A card such as "AS" or "10H": rank text followed by the suit letter S, H, D or C. */
export type Card = string;

export interface HandSizes {
  north: number;
  east: number;
  south: number;
  west: number;
}

export interface PlayedCard {
  seat: Seat;
  card: Card;
}

export interface CompletedTrick {
  dealNumber: number;
  trickNumber: number;
  leader: Seat;
  plays: PlayedCard[];
  winner: Seat;
}

export interface BidEntry {
  seat: Seat;
  action: string;
  contract: ContractKind | null;
}

export interface AnnouncementSpec {
  kind: "sequence" | "carre";
  suit: SuitName | null;
  rank: string;
  length: number;
}

export interface PublicAnnouncement {
  seat: Seat;
  kind: "sequence" | "carre";
  length: number;
  revealed: AnnouncementSpec | null;
  points: number | null;
}

export interface Score {
  northSouth: number;
  eastWest: number;
}

export interface IntentDto {
  kind: "bid" | "pass" | "contra" | "recontra" | "announce" | "play";
  seat: Seat;
  contract?: ContractKind | null;
  announcement?: AnnouncementSpec | null;
  card?: Card | null;
}

export interface TeamHand {
  /** Every card the team won in the hand, in trick order. */
  wonCards: Card[];
  cardPoints: number;
  lastTrickBonus: number;
  announcementPoints: number;
  belotePoints: number;
  allTricksBonus: number;
  rawTotal: number;
  /** Game points awarded to the team for the hand. */
  gamePoints: number;
}

export interface HeldOverSummary {
  before: number;
  awarded: number;
  awardedTo: Team | null;
  newlyHeld: number;
  after: number;
}

/** The most recently scored hand; played cards and engine-computed numbers only. */
export interface HandSummary {
  dealNumber: number;
  contract: { kind: ContractKind; declarer: Seat; multiplier: number };
  declaringTeam: Team;
  outcome: "made" | "failed" | "tied";
  teams: { northSouth: TeamHand; eastWest: TeamHand };
  /** Running totals after this hand. */
  matchScore: Score;
  heldOver: HeldOverSummary;
  winner: Team | null;
}

export interface TableView {
  seat: Seat;
  phase: Phase;
  dealer: Seat;
  toAct: Seat;
  dealNumber: number;
  ownHand: Card[];
  handSizes: HandSizes;
  biddingHistory: BidEntry[];
  standingBid: ContractKind | null;
  standingBidder: Seat | null;
  contra: boolean;
  recontra: boolean;
  contract: { kind: ContractKind; declarer: Seat; multiplier: number } | null;
  leader: Seat;
  currentTrick: PlayedCard[];
  lastCompletedTrick: CompletedTrick | null;
  completedTricks: CompletedTrick[];
  announcements: PublicAnnouncement[];
  ownDeclared: AnnouncementSpec[];
  belotes: { seat: Seat; suit: SuitName }[];
  matchScore: Score;
  heldOverPoints: number;
  winner: Team | null;
  /** Null before the first hand is scored; older servers omit it. */
  lastHand?: HandSummary | null;
  legalActions: IntentDto[];
}

export interface TableUpdate {
  protocolVersion: number;
  version: number;
  finished: boolean;
  view: TableView;
}

export interface TableClosed {
  protocolVersion: number;
  reason: string;
}

export interface Rejection {
  category: string;
  code: string;
  detail?: string;
}

export interface CreateTableResult {
  ok: boolean;
  tableId?: string | null;
  rejection?: Rejection | null;
}

export interface TakeSeatResult {
  ok: boolean;
  version?: number | null;
  rejection?: Rejection | null;
}

export interface IntentResult {
  accepted: boolean;
  version?: number | null;
  rejection?: Rejection | null;
}

export interface GetViewResult {
  update?: TableUpdate | null;
  rejection?: Rejection | null;
}

export function isSupportedUpdate(update: { protocolVersion?: number } | null | undefined): boolean {
  return !!update && update.protocolVersion === PROTOCOL_VERSION;
}

export type ConnectionStatus = "connecting" | "connected" | "reconnecting" | "unavailable";
