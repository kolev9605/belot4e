import type {
  AnnouncementSpec,
  CompletedTrick,
  HandSummary,
  IntentDto,
  PlayedCard,
  Seat,
  Team,
  TableView,
} from "./types";

/**
 * The table UI is drawn from the point of view of "south": the person at the bottom, their partner
 * on top. In a room a person can hold any seat, so views are rotated at the boundary - every seat and
 * team in a received view is relabelled so the viewer becomes "south" - and outgoing intents are
 * rotated back. Everything downstream of this module can keep assuming the viewer sits south.
 */

const ORDER: Seat[] = ["north", "east", "south", "west"];

/** How many places clockwise a seat moves so that <viewer> lands on south. */
function shiftFor(viewer: Seat): number {
  return (ORDER.indexOf("south") - ORDER.indexOf(viewer) + 4) % 4;
}

export function rotateSeat(seat: Seat, viewer: Seat): Seat {
  return ORDER[(ORDER.indexOf(seat) + shiftFor(viewer)) % 4]!;
}

/** The inverse of {@link rotateSeat}: the real seat for a viewer-relative one. */
export function unrotateSeat(seat: Seat, viewer: Seat): Seat {
  return ORDER[(ORDER.indexOf(seat) - shiftFor(viewer) + 8) % 4]!;
}

/** True when the viewer sits east or west, so the two teams swap labels. */
function swapsTeams(viewer: Seat): boolean {
  return viewer === "east" || viewer === "west";
}

function rotateTeam(team: Team, viewer: Seat): Team {
  if (!swapsTeams(viewer)) return team;
  return team === "northSouth" ? "eastWest" : "northSouth";
}

function nullableTeam(team: Team | null, viewer: Seat): Team | null {
  return team === null ? null : rotateTeam(team, viewer);
}

function nullableSeat(seat: Seat | null, viewer: Seat): Seat | null {
  return seat === null ? null : rotateSeat(seat, viewer);
}

function played(plays: PlayedCard[], viewer: Seat): PlayedCard[] {
  return plays.map((p) => ({ ...p, seat: rotateSeat(p.seat, viewer) }));
}

function trick(t: CompletedTrick | null, viewer: Seat): CompletedTrick | null {
  if (!t) return null;
  return {
    ...t,
    leader: rotateSeat(t.leader, viewer),
    plays: played(t.plays, viewer),
    winner: rotateSeat(t.winner, viewer),
  };
}

function rotateScore<T extends { northSouth: number; eastWest: number }>(score: T, viewer: Seat): T {
  return swapsTeams(viewer) ? { ...score, northSouth: score.eastWest, eastWest: score.northSouth } : score;
}

function rotateSummary(summary: HandSummary | null | undefined, viewer: Seat): HandSummary | null | undefined {
  if (!summary) return summary;
  const swap = swapsTeams(viewer);
  return {
    ...summary,
    contract: { ...summary.contract, declarer: rotateSeat(summary.contract.declarer, viewer) },
    declaringTeam: rotateTeam(summary.declaringTeam, viewer),
    teams: swap
      ? { northSouth: summary.teams.eastWest, eastWest: summary.teams.northSouth }
      : summary.teams,
    matchScore: rotateScore(summary.matchScore, viewer),
    heldOver: { ...summary.heldOver, awardedTo: nullableTeam(summary.heldOver.awardedTo, viewer) },
    winner: nullableTeam(summary.winner, viewer),
  };
}

function rotateIntent(intent: IntentDto, from: (seat: Seat) => Seat): IntentDto {
  return { ...intent, seat: from(intent.seat) };
}

/** Relabels a received view so its owner is "south". The identity when the viewer already sits south. */
export function rotateView(view: TableView): TableView {
  const viewer = view.seat;
  if (viewer === "south") return view;
  const rotatedSizes = { north: 0, east: 0, south: 0, west: 0 };
  for (const seat of ORDER) rotatedSizes[rotateSeat(seat, viewer)] = view.handSizes[seat];
  const spec = (a: AnnouncementSpec) => a;
  return {
    ...view,
    seat: "south",
    dealer: rotateSeat(view.dealer, viewer),
    toAct: rotateSeat(view.toAct, viewer),
    handSizes: rotatedSizes,
    biddingHistory: view.biddingHistory.map((b) => ({ ...b, seat: rotateSeat(b.seat, viewer) })),
    standingBidder: nullableSeat(view.standingBidder, viewer),
    contract: view.contract ? { ...view.contract, declarer: rotateSeat(view.contract.declarer, viewer) } : null,
    leader: rotateSeat(view.leader, viewer),
    currentTrick: played(view.currentTrick, viewer),
    lastCompletedTrick: trick(view.lastCompletedTrick, viewer),
    completedTricks: view.completedTricks.map((t) => trick(t, viewer)!),
    announcements: view.announcements.map((a) => ({
      ...a,
      seat: rotateSeat(a.seat, viewer),
      revealed: a.revealed ? spec(a.revealed) : a.revealed,
    })),
    belotes: view.belotes.map((b) => ({ ...b, seat: rotateSeat(b.seat, viewer) })),
    matchScore: rotateScore(view.matchScore, viewer),
    winner: nullableTeam(view.winner, viewer),
    lastHand: rotateSummary(view.lastHand, viewer),
    legalActions: view.legalActions.map((a) => rotateIntent(a, (s) => rotateSeat(s, viewer))),
  };
}

/** Turns an intent built from a rotated view back into the seat the server knows. */
export function unrotateIntent(intent: IntentDto, viewer: Seat | null): IntentDto {
  if (!viewer || viewer === "south") return intent;
  return rotateIntent(intent, (s) => unrotateSeat(s, viewer));
}
