import { describe, expect, it } from "vitest";
import { diffViews, type PresentationStep } from "../src/presentation/steps";
import { compressionScale, stepDuration } from "../src/presentation/usePresenter";
import { DEFAULT_PACING, INSTANT_PACING } from "../src/theme/motion";
import type { CompletedTrick, TableView } from "../src/protocol/types";
import { makeView } from "./fixtures";

const kinds = (prev: TableView | null, next: TableView) => diffViews(prev, next).map((s) => s.kind);

function trick(n: number, winner: CompletedTrick["winner"] = "west", deal = 1): CompletedTrick {
  return {
    dealNumber: deal,
    trickNumber: n,
    leader: "east",
    plays: [
      { seat: "east", card: "KD" },
      { seat: "south", card: "7D" },
      { seat: "west", card: "AD" },
      { seat: "north", card: "9D" },
    ],
    winner,
  };
}

describe("diffViews", () => {
  it("a first view of a fresh hand is a deal followed by a settle", () => {
    expect(kinds(null, makeView({ currentTrick: [] }))).toEqual(["deal", "settle"]);
  });

  it("a new deal number or a new dealer is a deal", () => {
    const prev = makeView();
    expect(kinds(prev, makeView({ dealNumber: 2 }))).toEqual(["deal", "settle"]);
    expect(kinds(prev, makeView({ dealer: "north" }))).toEqual(["deal", "settle"]);
  });

  it("a new game after a finished one is a deal even when the deal number repeats", () => {
    const prev = makeView({ phase: "finished" });
    expect(kinds(prev, makeView())).toEqual(["deal", "settle"]);
  });

  it("one more card in the trick is a play", () => {
    const prev = makeView({ currentTrick: [{ seat: "west", card: "9H" }] });
    const next = makeView({
      currentTrick: [
        { seat: "west", card: "9H" },
        { seat: "north", card: "JH" },
      ],
    });
    expect(kinds(prev, next)).toEqual(["play"]);
  });

  it("a completed trick is its fourth card, a hold, a sweep and a settle, in that order", () => {
    const prev = makeView({ currentTrick: trick(1).plays.slice(0, 3) });
    const finished = trick(1);
    const next = makeView({ currentTrick: [], completedTricks: [finished], lastCompletedTrick: finished });
    const steps = diffViews(prev, next);
    expect(steps.map((s) => s.kind)).toEqual(["play", "hold", "collect", "settle"]);
    expect(steps[1]!.frame.trick).toMatchObject({ mode: "hold", winner: "west" });
    expect(steps[2]!.frame.trick).toMatchObject({ mode: "collect", winner: "west" });
    expect(steps[3]!.frame.trick.plays).toEqual([]);
  });

  it("two tricks coalesced into one update both get their full sequence", () => {
    const t1 = trick(1, "west");
    const t2 = trick(2, "east");
    const prev = makeView({ currentTrick: [] });
    const next = makeView({ currentTrick: [], completedTricks: [t1, t2], lastCompletedTrick: t2 });
    expect(kinds(prev, next)).toEqual(["play", "hold", "collect", "play", "hold", "collect", "settle"]);
  });

  it("the last trick of a hand is shown before the next deal", () => {
    const prev = makeView({ currentTrick: trick(8).plays.slice(0, 3), completedTricks: [] });
    const next = makeView({ dealNumber: 2, lastCompletedTrick: trick(8, "south", 1), completedTricks: [] });
    expect(kinds(prev, next)).toEqual(["play", "hold", "collect", "deal", "settle"]);
  });

  it("the second deal after bidding adds cards to the same hand", () => {
    const prev = makeView({ phase: "bidding", ownHand: ["AS", "10S", "QH", "7H", "8C"] });
    const next = makeView({ phase: "bidding", ownHand: ["AS", "10S", "QH", "7H", "8C", "9D", "JD", "KD"] });
    const steps = diffViews(prev, next);
    expect(steps.map((s) => s.kind)).toEqual(["deal", "settle"]);
    expect(steps[0]!.frame.perSeat).toBe(3);
  });

  it("an unrelated change is a snap and an identical view is nothing", () => {
    const prev = makeView();
    expect(kinds(prev, makeView({ matchScore: { northSouth: 40, eastWest: 51 } }))).toEqual(["snap"]);
    expect(kinds(prev, makeView())).toEqual([]);
  });
});

describe("step timing", () => {
  const frame = diffViews(null, makeView())[0]!.frame;
  const step = (kind: PresentationStep["kind"]): PresentationStep => ({ kind, frame });

  it("uses the configured durations and zero when instant", () => {
    expect(stepDuration(step("hold"), DEFAULT_PACING)).toBe(DEFAULT_PACING.trickHoldMs);
    expect(stepDuration(step("collect"), DEFAULT_PACING)).toBe(DEFAULT_PACING.collectMs);
    expect(stepDuration(step("hold"), INSTANT_PACING)).toBe(0);
  });

  it("compresses only when more than three steps are waiting, never below 40 %", () => {
    expect(compressionScale(3)).toBe(1);
    expect(compressionScale(6)).toBeCloseTo(0.5);
    expect(compressionScale(100)).toBe(0.4);
  });
});
