import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

beforeEach(() => {
  window.localStorage.clear();
});

describe("table rendering (US1)", () => {
  it("shows the start control when idle and connected", async () => {
    await mount();
    expect(screen.getByRole("button", { name: "Start game" })).toBeEnabled();
    expect(screen.getByTestId("connection-banner")).toHaveAttribute("data-status", "connected");
  });

  it("starts a game: creates the table, takes South, then shows the dealt hand", async () => {
    const client = await mount();
    client.viewResult = { update: makeUpdate(0) };
    await userEvent.click(screen.getByRole("button", { name: "Start game" }));
    expect(client.calls).toEqual(expect.arrayContaining(["createTable", "takeSeat", "getView"]));
    expect(await screen.findByRole("button", { name: "Queen of Hearts, playable" })).toBeEnabled();
  });

  it("renders seats, counts, trick, scores and turn from the view", async () => {
    const client = await mount();
    await push(
      client,
      makeUpdate(0, {
        currentTrick: [
          { seat: "west", card: "9H" },
          { seat: "north", card: "JH" },
        ],
        handSizes: { north: 4, east: 5, south: 5, west: 4 },
      }),
    );
    for (const seat of ["north", "east", "south", "west"]) {
      expect(screen.getByTestId(`seat-${seat}`)).toBeInTheDocument();
    }
    expect(within(screen.getByTestId("seat-north")).getByText("4 cards")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-south")).getByText("5 cards")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Nine of Hearts" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Jack of Hearts" })).toBeInTheDocument();
    expect(screen.getByTestId("score-ns")).toHaveTextContent("34");
    expect(screen.getByTestId("score-ew")).toHaveTextContent("51");
    expect(screen.getByTestId("status")).toHaveTextContent("Your turn");
    // Opponents' cards are never shown face up.
    expect(screen.queryAllByRole("img").length).toBe(2);
  });

  it("keeps the completed trick and its winner visible", async () => {
    const client = await mount();
    await push(
      client,
      makeUpdate(0, {
        legalActions: [],
        toAct: "east",
        currentTrick: [],
        lastCompletedTrick: {
          dealNumber: 1,
          trickNumber: 2,
          leader: "east",
          plays: [
            { seat: "east", card: "KD" },
            { seat: "south", card: "7D" },
            { seat: "west", card: "AD" },
            { seat: "north", card: "9D" },
          ],
          winner: "west",
        },
      }),
    );
    expect(screen.getAllByRole("img").length).toBe(4);
    expect(screen.getByText("Trick won by West")).toBeInTheDocument();
  });

  it("offers legal bids during bidding and echoes the chosen action", async () => {
    const client = await mount();
    await push(
      client,
      makeUpdate(0, {
        phase: "bidding",
        contract: null,
        biddingHistory: [{ seat: "west", action: "pass", contract: null }],
        legalActions: [
          { kind: "pass", seat: "south" },
          { kind: "bid", seat: "south", contract: "hearts" },
        ],
      }),
    );
    expect(screen.getByText("West: pass")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Bid Hearts" }));
    expect(client.submitted).toEqual([{ kind: "bid", seat: "south", contract: "hearts" }]);
  });

  it("shows the result and play again when the game finishes", async () => {
    const client = await mount();
    await push(
      client,
      makeUpdate(40, { phase: "finished", legalActions: [], winner: "northSouth", matchScore: { northSouth: 151, eastWest: 90 } }, true),
    );
    const result = screen.getByTestId("result");
    expect(result).toHaveTextContent("North–South win");
    expect(result).toHaveTextContent("151 – 90");
    expect(screen.getByRole("button", { name: "Play again" })).toBeEnabled();
    for (const card of screen.getAllByRole("button", { name: /playable/ })) {
      expect(card).toBeDisabled();
    }
  });
});
