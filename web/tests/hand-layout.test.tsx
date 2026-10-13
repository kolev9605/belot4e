import { act, cleanup, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { App } from "../src/App";
import { FakeHubClient, makeUpdate } from "./fixtures";

async function mountWith(hand: string[], overrides: Parameters<typeof makeUpdate>[1] = {}) {
  window.localStorage.setItem("belote.language", "en");
  const client = new FakeHubClient();
  render(<App client={client} />);
  await act(async () => {
    client.status("connected");
  });
  await act(async () => {
    client.push(makeUpdate(0, { ownHand: hand, handSizes: { north: 8, east: 8, south: hand.length, west: 8 }, ...overrides }));
  });
  return client;
}

beforeEach(() => {
  window.localStorage.clear();
});

describe("hand layout (spec 006 US2)", () => {
  it("keeps every card in one non-wrapping row, whatever the suit mix", async () => {
    for (const hand of [
      ["AS", "10S", "KS", "QS", "JS", "9S", "8S", "7S"],
      ["AS", "10H", "KD", "QC", "JS", "9H", "8D", "7C"],
      ["AS", "KH", "9C"],
    ]) {
      await mountWith(hand);
      const row = screen.getByTestId("hand");
      expect(row.className).toContain("flex-nowrap");
      expect(row.className).not.toContain("flex-wrap ");
      expect(within(row).getAllByRole("button")).toHaveLength(hand.length);
      cleanup();
    }
  });

  it("never lifts, offsets or re-spaces a card because of its suit, rank or playability", async () => {
    await mountWith(["AS", "10H", "KD", "QC", "JS", "9H", "8D", "7C"], {
      contract: { kind: "hearts", declarer: "east", multiplier: 1 },
      legalActions: [
        { kind: "play", seat: "south", card: "10H" },
        { kind: "play", seat: "south", card: "9H" },
      ],
    });
    const cards = within(screen.getByTestId("hand")).getAllByRole("button");
    for (const card of cards) {
      // Only the pointed-at card may lift (hover/focus variants); none sits raised or lowered at rest.
      const resting = card.className.split(/\s+/).filter((c) => !c.includes(":"));
      expect(resting.some((c) => /translate|top-|bottom-|mt-|mb-/.test(c))).toBe(false);
    }
    // Highlighting is by ring/opacity only.
    const playable = screen.getByRole("button", { name: "Ten of Hearts, playable" });
    expect(playable.className).toContain("ring-accent");
  });

  it("gives every suit group the same structure, so a trump marker never changes a group's height", async () => {
    await mountWith(["AS", "10H", "KD", "QC"], { contract: { kind: "hearts", declarer: "east", multiplier: 1 } });
    const groups = ["S", "H", "C", "D"].map((s) => screen.getByTestId(`suit-group-${s}`));
    for (const group of groups) {
      expect(group.children).toHaveLength(2); // the cards, then the marker
      expect(group.className).toContain("flex-col");
    }
    expect(screen.getByTestId("suit-group-H").lastElementChild).toHaveAttribute("data-trump", "true");
    expect(screen.getByTestId("suit-group-S").lastElementChild).not.toHaveAttribute("data-trump");
  });

  it("uses the same constant overlap inside every group and a fixed gap between groups", async () => {
    await mountWith(["AS", "KS", "10H", "9H", "QC"]);
    const spades = within(screen.getByTestId("suit-group-S")).getAllByRole("listitem");
    const hearts = within(screen.getByTestId("suit-group-H")).getAllByRole("listitem");
    expect(spades[0]!.style.marginLeft).toBe("0px");
    expect(spades[1]!.style.marginLeft).toBe("var(--card-overlap)");
    expect(hearts[1]!.style.marginLeft).toBe("var(--card-overlap)");
    expect(screen.getByTestId("suit-group-H").style.marginLeft).toBe("var(--suit-gap)");
    expect(screen.getByTestId("suit-group-C").style.marginLeft).toBe("var(--suit-gap)");
    expect(screen.getByTestId("suit-group-S").style.marginLeft).toBe("0px");
  });
});
