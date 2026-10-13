import { act, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { App } from "../src/App";
import { FakeHubClient, makeUpdate } from "./fixtures";

async function mount(client = new FakeHubClient()) {
  window.localStorage.setItem("belote.language", "en");
  render(<App client={client} />);
  await act(async () => {
    client.status("connected");
  });
  return client;
}

async function push(client: FakeHubClient, update: ReturnType<typeof makeUpdate>) {
  await act(async () => {
    client.push(update);
  });
}

function handCards(): string[] {
  const hand = screen.getByTestId("hand");
  return within(hand)
    .getAllByRole("button")
    .map((b) => b.getAttribute("data-card") ?? "");
}

beforeEach(() => {
  window.localStorage.clear();
});

describe("hand order (US1)", () => {
  // The server sends strength order within a suit but suits interleaved.
  const ownHand = ["AD", "JH", "10S", "9H", "KC", "AS", "QD"];

  it("groups by suit in the fixed order and keeps the server's order inside each suit", async () => {
    const client = await mount();
    await push(client, makeUpdate(1, { ownHand, legalActions: [] }));
    // Server order within suit is preserved: spades 10S before AS here only because it arrived first.
    expect(handCards()).toEqual(["10S", "AS", "JH", "9H", "KC", "AD", "QD"]);
  });

  it("does not reshuffle the remaining cards after a card is played", async () => {
    const client = await mount();
    await push(client, makeUpdate(1, { ownHand, legalActions: [] }));
    const before = handCards();
    await push(client, makeUpdate(2, { ownHand: ownHand.filter((c) => c !== "9H"), legalActions: [] }));
    expect(handCards()).toEqual(before.filter((c) => c !== "9H"));
  });

  it("tags the trump group without moving it when the contract changes", async () => {
    const client = await mount();
    await push(client, makeUpdate(1, { ownHand, legalActions: [], contract: { kind: "hearts", declarer: "east", multiplier: 1 } }));
    const positionsHearts = handCards();
    expect(within(screen.getByTestId("suit-group-H")).getByText("Trumps")).toBeInTheDocument();
    expect(screen.getByTestId("suit-group-S")).not.toHaveTextContent("Trumps");

    await push(client, makeUpdate(2, { ownHand, legalActions: [], contract: { kind: "spades", declarer: "east", multiplier: 1 } }));
    expect(handCards()).toEqual(positionsHearts);
    expect(within(screen.getByTestId("suit-group-S")).getByText("Trumps")).toBeInTheDocument();
    expect(screen.getByTestId("suit-group-H")).not.toHaveTextContent("Trumps");
  });
});
