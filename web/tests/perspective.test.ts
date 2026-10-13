import { describe, expect, it } from "vitest";
import { rotateSeat, rotateView, unrotateIntent, unrotateSeat } from "../src/protocol/perspective";
import type { Seat } from "../src/protocol/types";
import { makeSummary, makeView } from "./fixtures";

const SEATS: Seat[] = ["north", "east", "south", "west"];

describe("seat rotation", () => {
  it("puts the viewer south and keeps the clockwise order and partners", () => {
    for (const viewer of SEATS) {
      expect(rotateSeat(viewer, viewer)).toBe("south");
      const partner = SEATS[(SEATS.indexOf(viewer) + 2) % 4]!;
      expect(rotateSeat(partner, viewer)).toBe("north");
      for (const seat of SEATS) {
        expect(unrotateSeat(rotateSeat(seat, viewer), viewer)).toBe(seat);
      }
    }
  });

  it("lays out a west viewer with east as partner, north on the left and south on the right", () => {
    expect(rotateSeat("west", "west")).toBe("south");
    expect(rotateSeat("east", "west")).toBe("north");
    expect(rotateSeat("north", "west")).toBe("west");
    expect(rotateSeat("south", "west")).toBe("east");
  });
});

describe("rotateView", () => {
  it("is the identity for a south viewer", () => {
    const view = makeView();
    expect(rotateView(view)).toBe(view);
  });

  it("relabels every seat and swaps teams for an east viewer", () => {
    const view = makeView({
      seat: "east",
      dealer: "north",
      toAct: "east",
      leader: "south",
      handSizes: { north: 1, east: 2, south: 3, west: 4 },
      contract: { kind: "hearts", declarer: "west", multiplier: 1 },
      currentTrick: [{ seat: "east", card: "AS" }],
      biddingHistory: [{ seat: "west", action: "bid", contract: "hearts" }],
      standingBidder: "west",
      matchScore: { northSouth: 10, eastWest: 70 },
      winner: "eastWest",
      legalActions: [{ kind: "play", seat: "east", card: "AS" }],
    });

    const rotated = rotateView(view);

    expect(rotated.seat).toBe("south");
    expect(rotated.toAct).toBe("south");
    expect(rotated.dealer).toBe("east");
    expect(rotated.leader).toBe("west");
    expect(rotated.handSizes).toEqual({ north: 4, east: 1, south: 2, west: 3 });
    expect(rotated.contract?.declarer).toBe("north");
    expect(rotated.currentTrick).toEqual([{ seat: "south", card: "AS" }]);
    expect(rotated.biddingHistory[0]?.seat).toBe("north");
    expect(rotated.standingBidder).toBe("north");
    expect(rotated.matchScore).toEqual({ northSouth: 70, eastWest: 10 });
    expect(rotated.winner).toBe("northSouth");
    expect(rotated.legalActions).toEqual([{ kind: "play", seat: "south", card: "AS" }]);
  });

  it("rotates the hand summary teams and declaring team together", () => {
    const view = makeView({
      seat: "west",
      lastHand: makeSummary({
        declaringTeam: "eastWest",
        matchScore: { northSouth: 39, eastWest: 66 },
        winner: "eastWest",
      }),
    });

    const hand = rotateView(view).lastHand!;

    expect(hand.declaringTeam).toBe("northSouth");
    expect(hand.matchScore).toEqual({ northSouth: 66, eastWest: 39 });
    expect(hand.winner).toBe("northSouth");
    expect(hand.contract.declarer).toBe("north");
    expect(hand.teams.northSouth.gamePoints).toBe(15);
  });

  it("sends an intent built on the rotated view back to the real seat", () => {
    const view = rotateView(makeView({ seat: "north", legalActions: [{ kind: "pass", seat: "north" }] }));
    const intent = view.legalActions[0]!;
    expect(intent.seat).toBe("south");
    expect(unrotateIntent(intent, "north").seat).toBe("north");
    expect(unrotateIntent(intent, "south")).toBe(intent);
    expect(unrotateIntent(intent, null)).toBe(intent);
  });
});
