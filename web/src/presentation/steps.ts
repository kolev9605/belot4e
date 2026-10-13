import type { PlayedCard, Seat, TableView } from "../protocol/types";

export type TrickMode = "live" | "hold" | "collect";

export interface TrickFrame {
  plays: PlayedCard[];
  winner: Seat | null;
  mode: TrickMode;
}

/** Everything the table draws at one moment. `view` may be older than the store's latest view. */
export interface Frame {
  view: TableView;
  trick: TrickFrame;
  /** True while the deal animation is running; cards then originate at `view.dealer`. */
  dealing: boolean;
  /** Cards each seat receives in this deal step (5 for the first deal, 3 for the second). */
  perSeat: number;
}

export type StepKind = "deal" | "play" | "hold" | "collect" | "snap" | "settle";

export interface PresentationStep {
  kind: StepKind;
  frame: Frame;
}

const EMPTY_TRICK: TrickFrame = { plays: [], winner: null, mode: "live" };

function liveTrick(view: TableView): TrickFrame {
  return { plays: view.currentTrick, winner: null, mode: "live" };
}

function frame(view: TableView, trick: TrickFrame, dealing = false, perSeat = 0): Frame {
  return { view, trick, dealing, perSeat };
}

function biggestHand(view: TableView): number {
  return Math.max(0, ...Object.values(view.handSizes));
}

/** The steps that show one finished trick: its fourth card, a readable hold, then the sweep. */
function trickSteps(view: TableView, plays: PlayedCard[], winner: Seat): PresentationStep[] {
  return [
    { kind: "play", frame: frame(view, { plays, winner: null, mode: "live" }) },
    { kind: "hold", frame: frame(view, { plays, winner, mode: "hold" }) },
    { kind: "collect", frame: frame(view, { plays, winner, mode: "collect" }) },
  ];
}

/** The table as it stands once every card of a deal has gone to the table. */
function emptiedHand(view: TableView): TableView {
  return {
    ...view,
    ownHand: [],
    handSizes: { north: 0, east: 0, south: 0, west: 0 },
    currentTrick: [],
    legalActions: [],
  };
}

/**
 * Turns the change from the previously presented view into the ordered steps that show it.
 * Only fields already in the projection are used (dealNumber, dealer, currentTrick, completedTricks).
 */
export function diffViews(prev: TableView | null, next: TableView): PresentationStep[] {
  if (!prev) {
    const fresh = next.completedTricks.length === 0 && next.currentTrick.length === 0;
    return fresh
      ? [
          { kind: "deal", frame: frame(next, EMPTY_TRICK, true, biggestHand(next)) },
          { kind: "settle", frame: frame(next, EMPTY_TRICK) },
        ]
      : [{ kind: "snap", frame: frame(next, liveTrick(next)) }];
  }

  const steps: PresentationStep[] = [];
  const newDeal = prev.phase === "finished" || next.dealNumber !== prev.dealNumber || next.dealer !== prev.dealer;

  if (newDeal) {
    // The last trick of a hand arrives in the same update as the next deal; show it first.
    const last = next.lastCompletedTrick;
    if (
      last &&
      last.dealNumber === prev.dealNumber &&
      prev.phase !== "finished" &&
      !prev.completedTricks.some((t) => t.trickNumber === last.trickNumber)
    ) {
      steps.push(...trickSteps(emptiedHand(prev), last.plays, last.winner));
    }
    steps.push({ kind: "deal", frame: frame(next, EMPTY_TRICK, true, biggestHand(next)) });
    steps.push({ kind: "settle", frame: frame(next, liveTrick(next)) });
    return steps;
  }

  if (next.completedTricks.length > prev.completedTricks.length) {
    for (const trick of next.completedTricks.slice(prev.completedTricks.length)) {
      steps.push(...trickSteps(next, trick.plays, trick.winner));
    }
    steps.push({ kind: "settle", frame: frame(next, liveTrick(next)) });
    return steps;
  }

  if (next.ownHand.length > prev.ownHand.length) {
    // The second deal (after bidding) adds cards to the same hand.
    const perSeat = next.ownHand.length - prev.ownHand.length;
    return [
      { kind: "deal", frame: frame(next, liveTrick(next), true, perSeat) },
      { kind: "settle", frame: frame(next, liveTrick(next)) },
    ];
  }

  if (next.currentTrick.length > prev.currentTrick.length) {
    return [{ kind: "play", frame: frame(next, liveTrick(next)) }];
  }

  if (JSON.stringify(prev) === JSON.stringify(next)) return [];
  return [{ kind: "snap", frame: frame(next, liveTrick(next)) }];
}
