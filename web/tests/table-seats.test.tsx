import { act, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { App } from "../src/App";
import { FakeHubClient, makeUpdate } from "./fixtures";

async function mount(language: "en" | "bg" = "en", client = new FakeHubClient()) {
  window.localStorage.setItem("belote.language", language);
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

describe("table seats (US3)", () => {
  it("marks exactly one dealer, with text, and moves it with the deal", async () => {
    const client = await mount();
    await push(client, makeUpdate(1, { dealer: "west" }));
    expect(screen.getAllByTestId("dealer-marker")).toHaveLength(1);
    expect(within(screen.getByTestId("seat-west")).getByTestId("dealer-marker")).toHaveTextContent("Dealer");

    await push(client, makeUpdate(2, { dealer: "north", dealNumber: 2 }));
    expect(screen.getAllByTestId("dealer-marker")).toHaveLength(1);
    expect(within(screen.getByTestId("seat-north")).getByTestId("dealer-marker")).toBeInTheDocument();
  });

  it("shows whose turn it is with text, not colour alone", async () => {
    const client = await mount();
    await push(client, makeUpdate(1, { toAct: "east", legalActions: [] }));
    const east = screen.getByTestId("seat-east");
    expect(east).toHaveAttribute("data-turn", "true");
    expect(east).toHaveTextContent("to act");
    expect(screen.getByTestId("seat-north")).not.toHaveTextContent("to act");
  });

  it("draws each opponent's cards as backs matching their hand size, never as faces", async () => {
    const client = await mount();
    await push(client, makeUpdate(1, { handSizes: { north: 3, east: 5, south: 5, west: 4 } }));
    expect(screen.getByTestId("fan-north")).toHaveAttribute("data-count", "3");
    expect(screen.getByTestId("fan-east")).toHaveAttribute("data-count", "5");
    expect(screen.getByTestId("fan-west")).toHaveAttribute("data-count", "4");
    expect(screen.getByTestId("fan-north").children).toHaveLength(3);
    expect(screen.queryByTestId("fan-south")).not.toBeInTheDocument();
  });

  it("labels partner and opponents and says which seats are on your team", async () => {
    const client = await mount();
    await push(client, makeUpdate(1));
    expect(within(screen.getByTestId("seat-north")).getByText("(Partner)")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-north")).getByText("Your team")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-south")).getByText("Your team")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-east")).getByText("Opponents")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-west")).getByText("Opponents")).toBeInTheDocument();
  });

  it("shows contract and both team scores without opening anything", async () => {
    const client = await mount();
    await push(client, makeUpdate(1));
    expect(screen.getByTestId("score-ns")).toHaveTextContent("34");
    expect(screen.getByTestId("score-ew")).toHaveTextContent("51");
    expect(screen.getByText(/Contract:/)).toHaveTextContent("Hearts");
  });

  it("localizes the new labels", async () => {
    const client = await mount("bg");
    await push(client, makeUpdate(1, { dealer: "west" }));
    expect(within(screen.getByTestId("seat-west")).getByTestId("dealer-marker")).not.toHaveTextContent("Dealer");
    expect(within(screen.getByTestId("seat-north")).getByText("Вашият отбор")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-east")).getByText("Противници")).toBeInTheDocument();
  });
});

describe("seat call history (spec 006 US4)", () => {
  const bidding = {
    phase: "bidding" as const,
    contract: null,
    legalActions: [{ kind: "pass" as const, seat: "south" as const }],
  };

  it("shows nothing before the first call", async () => {
    const client = await mount();
    await push(client, makeUpdate(1, { ...bidding, biddingHistory: [] }));
    expect(screen.queryByTestId("seat-calls-west")).toBeNull();
  });

  it("lists each seat's calls in order with the latest marked", async () => {
    const client = await mount();
    await push(
      client,
      makeUpdate(1, {
        ...bidding,
        biddingHistory: [
          { seat: "west", action: "pass", contract: null },
          { seat: "north", action: "bid", contract: "hearts" },
          { seat: "east", action: "bid", contract: "spades" },
          { seat: "south", action: "pass", contract: null },
          { seat: "west", action: "bid", contract: "noTrump" },
        ],
      }),
    );
    const west = screen.getByTestId("seat-calls-west");
    const items = within(west).getAllByRole("listitem");
    expect(items.map((li) => li.textContent)).toEqual(["Pass", "No trumps"]);
    expect(items[1]).toHaveAttribute("data-latest", "true");
    expect(items[0]).not.toHaveAttribute("data-latest");
    expect(within(screen.getByTestId("seat-calls-north")).getByText("Hearts")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-calls-south")).getByText("Pass")).toBeInTheDocument();
  });

  it("keeps the calls once play begins and clears them on the next deal", async () => {
    const client = await mount();
    const history = [
      { seat: "west" as const, action: "pass", contract: null },
      { seat: "east" as const, action: "bid", contract: "hearts" as const },
    ];
    await push(client, makeUpdate(1, { biddingHistory: history }));
    expect(screen.getByTestId("seat-calls-east")).toHaveTextContent("Hearts");
    await push(client, makeUpdate(2, { dealNumber: 2, biddingHistory: [] }));
    expect(screen.queryByTestId("seat-calls-east")).toBeNull();
  });

  it("localizes the calls", async () => {
    const client = await mount("bg");
    await push(client, makeUpdate(1, { biddingHistory: [{ seat: "west", action: "pass", contract: null }] }));
    expect(screen.getByTestId("seat-calls-west")).toHaveTextContent("Пас");
  });
});
