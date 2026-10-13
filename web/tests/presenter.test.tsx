import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "../src/App";
import { DEFAULT_PACING } from "../src/theme/motion";
import type { CompletedTrick } from "../src/protocol/types";
import { FakeHubClient, makeUpdate } from "./fixtures";

const done: CompletedTrick = {
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
};

async function mount(client = new FakeHubClient()) {
  window.localStorage.setItem("belote.language", "en");
  render(<App client={client} pacing={DEFAULT_PACING} />);
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

async function advance(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

/** Version 0 shows the opening deal; wait for it to finish so later steps start from a settled table. */
async function settleOpeningDeal() {
  await advance(5000);
}

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  window.localStorage.clear();
});

afterEach(() => {
  vi.useRealTimers();
});

describe("presenter pacing (US2)", () => {
  it("holds a completed trick for the hold duration, then sweeps it away", async () => {
    const client = await mount();
    await push(client, makeUpdate(0, { currentTrick: done.plays.slice(0, 3), legalActions: [] }));
    await settleOpeningDeal();
    await push(
      client,
      makeUpdate(1, {
        currentTrick: [],
        completedTricks: [done],
        lastCompletedTrick: done,
        legalActions: [],
        toAct: "west",
      }),
    );

    expect(screen.getAllByRole("img")).toHaveLength(4);
    await advance(DEFAULT_PACING.playTravelMs + 50);
    expect(screen.getByText("Trick won by West")).toBeInTheDocument();
    expect(screen.getAllByRole("img")).toHaveLength(4);

    // Still on the table close to the end of the hold...
    await advance(DEFAULT_PACING.trickHoldMs - 200);
    expect(screen.getAllByRole("img")).toHaveLength(4);

    // ...and gone once the sweep has finished.
    await advance(200 + DEFAULT_PACING.collectMs + 50);
    expect(screen.queryAllByRole("img")).toHaveLength(0);
  });

  it("keeps controls disabled until the table settles, then enables them", async () => {
    const client = await mount();
    await push(client, makeUpdate(0, { currentTrick: done.plays.slice(0, 3), legalActions: [] }));
    await settleOpeningDeal();
    await push(
      client,
      makeUpdate(1, {
        currentTrick: [],
        completedTricks: [done],
        lastCompletedTrick: done,
        toAct: "south",
        legalActions: [{ kind: "play", seat: "south", card: "QH" }],
      }),
    );
    const card = () => screen.getByRole("button", { name: /Queen of Hearts/ });
    expect(card()).toBeDisabled();
    await advance(DEFAULT_PACING.playTravelMs + DEFAULT_PACING.trickHoldMs + DEFAULT_PACING.collectMs + 100);
    expect(card()).toBeEnabled();
    await userEvent.setup({ advanceTimers: vi.advanceTimersByTime }).click(card());
    expect(client.submitted).toHaveLength(1);
  });

  it("presents queued views in order and ends on the latest", async () => {
    const client = await mount();
    await push(client, makeUpdate(0, { currentTrick: [], legalActions: [] }));
    await settleOpeningDeal();
    const seats = ["west", "north", "east"] as const;
    const cards = ["9H", "JH", "KH"];
    for (let i = 0; i < 3; i++) {
      await push(
        client,
        makeUpdate(i + 1, {
          currentTrick: seats.slice(0, i + 1).map((seat, j) => ({ seat, card: cards[j]! })),
          legalActions: [],
          toAct: seats[i + 1] ?? "south",
        }),
      );
    }
    await advance(2000);
    expect(screen.getAllByRole("img")).toHaveLength(3);
  });

  it("drops the queue and shows the latest state when the connection is lost", async () => {
    const client = await mount();
    await push(client, makeUpdate(0, { currentTrick: done.plays.slice(0, 3), legalActions: [] }));
    await settleOpeningDeal();
    await push(
      client,
      makeUpdate(1, { currentTrick: [], completedTricks: [done], lastCompletedTrick: done, legalActions: [] }),
    );
    await act(async () => {
      client.status("reconnecting");
    });
    await advance(10);
    expect(screen.getByTestId("connection-banner")).toHaveAttribute("data-status", "reconnecting");
    expect(screen.queryByText("Trick won by West")).not.toBeInTheDocument();
  });
});
