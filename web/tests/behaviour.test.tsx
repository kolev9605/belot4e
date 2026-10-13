import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

describe("rules are server-driven (US2)", () => {
  it("disables every card and offers no bids when it is not the player's turn", async () => {
    const client = await mount();
    await push(client, makeUpdate(0, { legalActions: [], toAct: "east" }));
    expect(screen.getByTestId("status")).toHaveTextContent("Waiting for East");
    // Another seat's turn: cards are shown normally (no playable/not-playable wording) and cannot be used.
    expect(screen.queryByRole("button", { name: /playable/ })).toBeNull();
    for (const card of screen.getAllByRole("button", { name: /of (Spades|Hearts|Clubs|Diamonds)$/ })) {
      expect(card).toBeDisabled();
    }
    expect(screen.queryByRole("button", { name: /^Bid / })).toBeNull();
  });

  it("enables only the cards the server lists as legal", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    expect(screen.getByRole("button", { name: "Queen of Hearts, playable" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Seven of Hearts, playable" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Ace of Spades, not playable" })).toBeDisabled();
  });

  it("explains a refusal in plain language and leaves the table unchanged", async () => {
    const client = await mount();
    client.intentResult = { accepted: false, rejection: { category: "illegal", code: "MustFollowSuit", detail: "dev text" } };
    await push(client, makeUpdate(0));
    await userEvent.click(screen.getByRole("button", { name: "Queen of Hearts, playable" }));
    expect(await screen.findByTestId("notice")).toHaveTextContent("You must follow the suit that was led.");
    expect(screen.queryByText("dev text")).toBeNull();
    // Still the same hand, and a legal move works afterwards.
    expect(screen.getByRole("button", { name: "Ace of Spades, not playable" })).toBeInTheDocument();
    client.intentResult = { accepted: true, version: 1 };
    await userEvent.click(screen.getByRole("button", { name: "Seven of Hearts, playable" }));
    expect(client.submitted.at(-1)).toEqual({ kind: "play", seat: "south", card: "7H" });
    await push(client, makeUpdate(1, { legalActions: [], toAct: "west", ownHand: ["AS", "10S", "QH", "8C"] }));
    expect(screen.queryByTestId("notice")).toBeNull();
  });

  it("sends a rapid double click only once", async () => {
    const client = await mount();
    let release: (r: { accepted: true; version: number }) => void = () => {};
    client.intentResult = () => new Promise((resolve) => (release = resolve));
    await push(client, makeUpdate(0));
    const card = screen.getByRole("button", { name: "Queen of Hearts, playable" });
    await userEvent.dblClick(card);
    expect(client.submitted).toHaveLength(1);
    expect(screen.getByTestId("status")).toHaveTextContent("Sending your move…");
    await act(async () => release({ accepted: true, version: 1 }));
    await push(client, makeUpdate(1, { legalActions: [], toAct: "west" }));
    expect(client.submitted).toHaveLength(1);
  });

  it("notes a voided hand when bidding restarts empty", async () => {
    const client = await mount();
    const bidding = { phase: "bidding" as const, contract: null, legalActions: [{ kind: "pass" as const, seat: "south" as const }] };
    await push(
      client,
      makeUpdate(0, {
        ...bidding,
        biddingHistory: [
          { seat: "west", action: "pass", contract: null },
          { seat: "north", action: "pass", contract: null },
          { seat: "east", action: "pass", contract: null },
          { seat: "south", action: "pass", contract: null },
        ],
      }),
    );
    await push(client, makeUpdate(1, { ...bidding, dealNumber: 2, biddingHistory: [] }));
    expect(screen.getByText(/the hand was voided and redealt/)).toBeInTheDocument();
  });
});

describe("connection handling (US3)", () => {
  it("reads the server view on connect before trusting anything", async () => {
    const client = await mount();
    expect(client.calls).toContain("getView");
  });

  it("ignores a stale 'no table' answer that arrives after the game started", async () => {
    const client = new FakeHubClient();
    let answerLate: () => void = () => {};
    const original = client.getView.bind(client);
    let first = true;
    client.getView = async () => {
      if (first) {
        first = false;
        await new Promise<void>((resolve) => (answerLate = resolve));
        return { rejection: { category: "notSeated", code: "NotSeated" } };
      }
      return original();
    };
    await mount("en", client); // connect triggers the slow initial read
    await push(client, makeUpdate(0)); // the game starts (pushed) before the stale answer arrives
    await act(async () => answerLate());
    expect(await screen.findByRole("button", { name: "Queen of Hearts, playable" })).toBeInTheDocument();
    expect(screen.queryByTestId("game-lost")).toBeNull();
  });

  it("restores a game found on connect", async () => {
    const client = new FakeHubClient();
    client.viewResult = { update: makeUpdate(7) };
    await mount("en", client);
    expect(await screen.findByRole("button", { name: "Queen of Hearts, playable" })).toBeInTheDocument();
  });

  it("keeps the table on screen, stale and inert, while the connection drops and comes back", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    await act(async () => client.status("reconnecting"));
    expect(screen.getByTestId("connection-banner")).toHaveAttribute("data-status", "reconnecting");
    // The game is not thrown away; input is simply off until the server confirms it is still there.
    expect(screen.queryByTestId("game-lost")).toBeNull();
    expect(screen.getByRole("button", { name: "Queen of Hearts, playable" })).toBeDisabled();

    // Back online with no table on the server (legacy single table): the game is lost, and a new one can start.
    await act(async () => client.status("connected"));
    expect(await screen.findByTestId("game-lost")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Start game" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: /playable/ })).toBeNull();
  });

  it("treats TableClosed as a lost game with a way to start another", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    await act(async () => client.close());
    expect(screen.getByTestId("game-lost")).toHaveTextContent("another window");
    expect(screen.getByRole("button", { name: "Start game" })).toBeEnabled();
  });

  it("recovers from a version gap by re-reading the view", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    client.calls.length = 0;
    client.viewResult = { update: makeUpdate(5) };
    await push(client, makeUpdate(3));
    expect(client.calls).toContain("getView");
  });

  it("cannot send moves while disconnected", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    // The table stays visible while the connection is down, but nothing can be sent.
    await act(async () => client.status("unavailable"));
    const card = screen.getByRole("button", { name: "Queen of Hearts, playable" });
    expect(card).toBeDisabled();
    await userEvent.click(card);
    expect(client.submitted).toHaveLength(0);
  });

  it("returns to the menu with an explanation when creating a room is refused", async () => {
    const client = await mount();
    window.localStorage.setItem("belote.profile.v1", JSON.stringify({ name: "Ivo" }));
    client.roomResult = { ok: false, rejection: { category: "state", code: "GameInProgress" } };
    await userEvent.click(screen.getByRole("button", { name: "Start game" }));
    expect(await screen.findByTestId("notice")).toHaveTextContent("The game is already in progress.");
    expect(screen.getByRole("button", { name: "Start game" })).toBeEnabled();
  });
});

describe("language and accessibility (US4)", () => {
  it("names every card by rank and suit in both languages", async () => {
    const client = await mount("bg");
    await push(client, makeUpdate(0));
    expect(screen.getByRole("button", { name: "Дама Купа, може да се играе" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Асо Пика, не може да се играе" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Смени езика на английски" })).toBeInTheDocument();
  });

  it("switches language at any time and keeps the game", async () => {
    const client = await mount("en");
    await push(client, makeUpdate(0));
    await userEvent.click(screen.getByRole("button", { name: "Switch language to Bulgarian" }));
    expect(screen.getByRole("button", { name: "Дама Купа, може да се играе" })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe("bg");
    expect(window.localStorage.getItem("belote.language")).toBe("bg");
  });

  it("announces turn and game state through a live region", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    expect(screen.getByTestId("live-region")).toHaveTextContent("It is your turn.");
  });

  it("does not rely on colour: unplayable cards say so in text", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    const card = screen.getByRole("button", { name: "Ace of Spades, not playable" });
    expect(card).toHaveTextContent("not playable");
  });

  it("every action can be reached with the keyboard", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    const user = userEvent.setup();
    const card = screen.getByRole("button", { name: "Queen of Hearts, playable" });
    for (let i = 0; i < 10 && document.activeElement !== card; i++) await user.tab();
    expect(card).toHaveFocus();
    await user.keyboard("{Enter}");
    expect(client.submitted.at(-1)).toEqual({ kind: "play", seat: "south", card: "QH" });
  });
});

describe("cards while bidding (spec 006 US3)", () => {
  it("shows every card normally: no playable wording, no struck line, no dimming", async () => {
    const client = await mount();
    await push(
      client,
      makeUpdate(0, {
        phase: "bidding",
        contract: null,
        biddingHistory: [],
        legalActions: [{ kind: "pass", seat: "south" }],
      }),
    );
    const hand = screen.getByTestId("hand");
    const cards = within(hand).getAllByRole("button");
    expect(cards).toHaveLength(5);
    for (const card of cards) {
      expect(card).toHaveAttribute("data-mode", "inert");
      expect(card.getAttribute("aria-label")).toMatch(/^(Ace|Ten|Queen|Seven|Eight) of /);
      expect(card).not.toHaveTextContent("not playable");
      expect(card.className).not.toMatch(/opacity-/);
    }
  });

  it("highlights legal cards and marks the rest unavailable only on the person's turn to play", async () => {
    const client = await mount();
    await push(client, makeUpdate(0));
    expect(screen.getByRole("button", { name: "Queen of Hearts, playable" })).toHaveAttribute("data-mode", "playable");
    expect(screen.getByRole("button", { name: "Ace of Spades, not playable" })).toHaveAttribute("data-mode", "unplayable");
  });
});
