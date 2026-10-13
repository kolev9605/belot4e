import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "../src/App";
import { resetStorageForTests } from "../src/session/storage";
import { FakeHubClient, makeRoomState, makeUpdate, ROOM_ID } from "./fixtures";

async function mount(client = new FakeHubClient(), path = "/") {
  window.localStorage.setItem("belote.language", "en");
  window.history.replaceState(null, "", path);
  render(<App client={client} />);
  await act(async () => {
    client.status("connected");
  });
  return client;
}

function saveName(name: string) {
  window.localStorage.setItem("belote.profile.v1", JSON.stringify({ name }));
}

beforeEach(() => {
  window.localStorage.clear();
  resetStorageForTests();
});

afterEach(() => {
  window.history.replaceState(null, "", "/");
  vi.restoreAllMocks();
});

describe("main menu: start and join (US1, US2)", () => {
  it("asks for a name the first time, then creates a room and shows the lobby with link and code", async () => {
    const client = new FakeHubClient();
    client.roomResult = { ok: true, roomId: ROOM_ID, state: makeRoomState() };
    await mount(client);

    await userEvent.click(screen.getByRole("button", { name: "Start game" }));
    await userEvent.clear(screen.getByTestId("name-input"));
    await userEvent.type(screen.getByTestId("name-input"), "Ivo");
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByTestId("lobby")).toBeInTheDocument();
    expect(client.created[0]?.name).toBe("Ivo");
    expect(client.created[0]?.token).toMatch(/^[0-9a-f]{32}$/);
    expect(screen.getByTestId("room-code")).toHaveTextContent(ROOM_ID);
    expect((screen.getByTestId("room-link") as HTMLInputElement).value).toContain(`/room/${ROOM_ID}`);
    expect(window.location.pathname).toBe(`/room/${ROOM_ID}`);
  });

  it("does not ask again once a name is remembered", async () => {
    saveName("Ivo");
    const client = new FakeHubClient();
    client.roomResult = { ok: true, roomId: ROOM_ID, state: makeRoomState() };
    await mount(client);

    await userEvent.click(screen.getByRole("button", { name: "Start game" }));

    expect(await screen.findByTestId("lobby")).toBeInTheDocument();
    expect(client.created[0]?.name).toBe("Ivo");
  });

  it("validates the name inline before sending anything", async () => {
    const client = await mount();
    await userEvent.click(screen.getByRole("button", { name: "Start game" }));
    await userEvent.clear(screen.getByTestId("name-input"));
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByTestId("name-error")).toHaveTextContent("1 to 20 characters");
    expect(client.created).toHaveLength(0);
  });

  it("the join box accepts a code or a pasted link and explains a bad one", async () => {
    await mount();
    await userEvent.click(screen.getByRole("button", { name: "Join game" }));

    await userEvent.type(screen.getByTestId("join-input"), "hello there");
    await userEvent.click(screen.getByRole("button", { name: "Join" }));
    expect(screen.getByTestId("join-error")).toHaveTextContent("doesn't look like a room code or link");

    await userEvent.clear(screen.getByTestId("join-input"));
    await userEvent.type(screen.getByTestId("join-input"), `https://belote.example/room/${ROOM_ID}`);
    await userEvent.click(screen.getByRole("button", { name: "Join" }));
    expect(window.location.pathname).toBe(`/room/${ROOM_ID}`);
  });

  it("offers to return to a saved room from the menu", async () => {
    window.localStorage.setItem("belote.session.v1", JSON.stringify({ roomId: ROOM_ID, lastSeen: 1 }));
    await mount();
    await userEvent.click(screen.getByTestId("return-to-game"));
    expect(window.location.pathname).toBe(`/room/${ROOM_ID}`);
  });
});

describe("opening a room link (US2)", () => {
  it("rejoins the room with the saved identity and shows the lobby with seats", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: true, roomId: ROOM_ID, state: makeRoomState() };
    await mount(client, `/room/${ROOM_ID}`);

    expect(await screen.findByTestId("lobby")).toBeInTheDocument();
    expect(client.roomCalls).toContain("rejoin");
    expect(screen.getByTestId("lobby-seat-name-north")).toHaveTextContent("Mira");
    expect(screen.getByTestId("start-room-game")).toBeEnabled();
  });

  it("joins as a new person with the remembered name when the room does not know this browser", async () => {
    saveName("Pavel");
    const client = new FakeHubClient();
    client.rejoinResult = { ok: false, rejection: { category: "identity", code: "NotInRoom" } };
    client.roomResult = {
      ok: true,
      roomId: ROOM_ID,
      state: makeRoomState({ you: { participantId: "p-3", seat: null, isHost: false } }, [
        { seat: "east", kind: "free", name: null, participantId: null, origin: null },
      ]),
    };
    await mount(client, `/room/${ROOM_ID}`);

    expect(await screen.findByTestId("lobby")).toBeInTheDocument();
    expect(client.joined[0]).toMatchObject({ roomId: ROOM_ID, name: "Pavel" });
    expect(screen.getByTestId("waiting-host")).toHaveTextContent("Waiting for Ivo");
  });

  it("asks for a name first when none is remembered", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: false, rejection: { category: "identity", code: "NotInRoom" } };
    client.roomResult = { ok: true, roomId: ROOM_ID, state: makeRoomState() };
    await mount(client, `/room/${ROOM_ID}`);

    expect(await screen.findByTestId("name-gate")).toBeInTheDocument();
    await userEvent.clear(screen.getByTestId("name-input"));
    await userEvent.type(screen.getByTestId("name-input"), "Dani");
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByTestId("lobby")).toBeInTheDocument();
    expect(client.joined[0]?.name).toBe("Dani");
    expect(JSON.parse(window.localStorage.getItem("belote.profile.v1")!).name).toBe("Dani");
  });

  it("explains a room that does not exist and offers a way back", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: false, rejection: { category: "room", code: "RoomNotFound" } };
    await mount(client, `/room/${ROOM_ID}`);

    expect(await screen.findByTestId("room-unavailable")).toHaveTextContent("could not find that room");
    await userEvent.click(screen.getByRole("button", { name: "Back to menu" }));
    expect(window.location.pathname).toBe("/");
  });

  it("tells a person the room is full", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: false, rejection: { category: "room", code: "RoomFull" } };
    await mount(client, `/room/${ROOM_ID}`);

    expect(await screen.findByTestId("room-unavailable")).toHaveTextContent("full");
  });

  it("lets a person pick a free seat and reports a lost race inline", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = {
      ok: true,
      roomId: ROOM_ID,
      state: makeRoomState({ you: { participantId: "p-3", seat: null, isHost: false } }, [
        { seat: "east", kind: "free", name: null },
      ]),
    };
    await mount(client, `/room/${ROOM_ID}`);

    client.roomResult = { ok: false, rejection: { category: "seat", code: "SeatTaken" } };
    await userEvent.click(await screen.findByTestId("take-east"));

    expect(client.roomCalls).toContain("claimSeat");
    expect(await screen.findByTestId("notice")).toHaveTextContent("That seat is already taken");
  });
});

describe("sharing (US1)", () => {
  it("copies the link and confirms, and falls back to a selectable link when copying fails", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: true, roomId: ROOM_ID, state: makeRoomState() };
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    await mount(client, `/room/${ROOM_ID}`);

    await userEvent.click(await screen.findByRole("button", { name: "Copy link" }));
    expect(writeText).toHaveBeenCalledWith(expect.stringContaining(`/room/${ROOM_ID}`));
    expect(screen.getByTestId("copy-status")).toHaveTextContent("Link copied");

    writeText.mockRejectedValueOnce(new Error("denied"));
    await userEvent.click(screen.getByRole("button", { name: "Copy link" }));
    expect(screen.getByTestId("copy-status")).toHaveTextContent("Could not copy");
    expect(screen.getByTestId("room-link")).toBeVisible();
  });
});

describe("playing in a room (US3)", () => {
  async function inGame(seat: "south" | "east" = "south") {
    const client = new FakeHubClient();
    const room = makeRoomState(
      { lifecycle: "ingame", you: { participantId: "p-1", seat, isHost: true } },
      seat === "east"
        ? [
            { seat: "south", kind: "human", name: "Ivo", participantId: "p-9" },
            { seat: "east", kind: "human", name: "Ivo", participantId: "p-1" },
            { seat: "north", kind: "human", name: "Mira", participantId: "p-2" },
            { seat: "west", kind: "bot", name: "Bot", participantId: null },
          ]
        : [],
    );
    client.rejoinResult = { ok: true, roomId: ROOM_ID, state: room };
    await mount(client, `/room/${ROOM_ID}`);
    return client;
  }

  it("shows real names at the seats instead of compass labels and marks the partner", async () => {
    const client = await inGame();
    await act(async () => client.push(makeUpdate(0)));

    expect(within(screen.getByTestId("seat-north")).getByText("Mira")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-north")).getByText("(Partner)")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-east")).getByText("Bot")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-south")).getByText("Ivo")).toBeInTheDocument();
  });

  it("draws the table from the viewer's seat and sends intents for the real seat", async () => {
    const client = await inGame("east");
    // The server addresses this person as "east"; legal actions carry that real seat.
    await act(async () =>
      client.push(
        makeUpdate(0, {
          seat: "east",
          toAct: "east",
          dealer: "south",
          handSizes: { north: 5, east: 5, south: 5, west: 5 },
          legalActions: [{ kind: "play", seat: "east", card: "QH" }],
        }),
      ),
    );

    // Partner (west) sits at the top and the hand is shown at the bottom.
    expect(within(screen.getByTestId("seat-north")).getByText("Bot")).toBeInTheDocument();
    expect(within(screen.getByTestId("seat-south")).getByText("Ivo")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Queen of Hearts, playable" }));
    expect(client.submitted.at(-1)).toEqual({ kind: "play", seat: "east", card: "QH" });
  });
});

describe("pause, reconnect and takeover (US4, US5)", () => {
  async function playing() {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: true, roomId: ROOM_ID, state: makeRoomState({ lifecycle: "ingame" }) };
    await mount(client, `/room/${ROOM_ID}`);
    await act(async () => client.push(makeUpdate(0)));
    return client;
  }

  it("shows who is missing with a countdown and lets another human skip the wait", async () => {
    const client = await playing();
    const deadline = new Date(Date.now() + 42_000).toISOString();
    await act(async () =>
      client.pushRoom(
        makeRoomState(
          {
            lifecycle: "ingame",
            revision: 2,
            missing: [{ participantId: "p-2", seat: "north", name: "Mira", deadlineUtc: deadline }],
          },
          [{ seat: "north", connection: "reconnecting", deadlineUtc: deadline }],
        ),
      ),
    );

    const overlay = await screen.findByTestId("pause-overlay");
    expect(within(overlay).getByText("Waiting for Mira")).toBeInTheDocument();
    expect(within(overlay).getByTestId("countdown")).toHaveTextContent(/\d+ s/);
    // Nothing can be played meanwhile.
    expect(screen.getByRole("button", { name: "Queen of Hearts, playable" })).toBeDisabled();

    client.roomResult = { ok: true, state: makeRoomState({ lifecycle: "ingame", revision: 3 }) };
    await userEvent.click(within(overlay).getByRole("button", { name: "Play with a bot now" }));
    expect(client.roomCalls).toContain("skipWait");
  });

  it("does not offer the skip to the missing player themselves", async () => {
    const client = await playing();
    const deadline = new Date(Date.now() + 30_000).toISOString();
    await act(async () =>
      client.pushRoom(
        makeRoomState({
          lifecycle: "ingame",
          revision: 2,
          missing: [{ participantId: "p-1", seat: "south", name: "Ivo", deadlineUtc: deadline }],
        }),
      ),
    );

    const overlay = await screen.findByTestId("pause-overlay");
    expect(within(overlay).queryByRole("button", { name: "Play with a bot now" })).toBeNull();
  });

  it("shows a reconnecting overlay with a way out while the connection is down", async () => {
    const client = await playing();
    await act(async () => client.status("reconnecting"));

    const overlay = await screen.findByTestId("reconnecting-overlay");
    expect(within(overlay).getByRole("button", { name: "Back to menu" })).toBeInTheDocument();
    expect(screen.getByTestId("connection-banner")).toHaveAttribute("data-status", "reconnecting");

    await act(async () => client.status("connected"));
    expect(screen.queryByTestId("reconnecting-overlay")).toBeNull();
    // Coming back rejoins automatically.
    expect(client.roomCalls.filter((c) => c === "rejoin").length).toBeGreaterThanOrEqual(2);
  });

  it("when taken over by another tab, stops acting until the person takes it back", async () => {
    const client = await playing();
    await act(async () => client.supersede());

    expect(await screen.findByTestId("superseded-overlay")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Queen of Hearts, playable" })).toBeDisabled();

    client.roomResult = { ok: true, state: makeRoomState({ lifecycle: "ingame", revision: 9 }) };
    await userEvent.click(screen.getByTestId("take-back"));
    expect(client.roomCalls).toContain("takeBack");
  });

  it("tells a returning player how many moves a bot made for them, once", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = {
      ok: true,
      roomId: ROOM_ID,
      state: makeRoomState({ lifecycle: "ingame" }),
      update: makeUpdate(4),
      awayMoves: 3,
    };
    await mount(client, `/room/${ROOM_ID}`);

    expect(await screen.findByTestId("recap")).toHaveTextContent("A bot played 3 moves for you");
    await userEvent.click(screen.getByRole("button", { name: "Got it" }));
    expect(screen.queryByTestId("recap")).toBeNull();
  });

  it("shows a bot that replaced a person with that person's name", async () => {
    const client = await playing();
    await act(async () =>
      client.pushRoom(
        makeRoomState({ lifecycle: "ingame", revision: 5 }, [
          { seat: "north", kind: "bot", name: "Bot", participantId: null, botFor: "Mira", origin: "replaced", connection: null },
        ]),
      ),
    );

    expect(within(screen.getByTestId("seat-north")).getByText("Bot (for Mira)")).toBeInTheDocument();
  });
});

describe("housekeeping (US7)", () => {
  it("leaves the room, clears the saved session and returns to the menu", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: true, roomId: ROOM_ID, state: makeRoomState() };
    await mount(client, `/room/${ROOM_ID}`);

    await userEvent.click(await screen.findByTestId("leave-room"));

    expect(client.roomCalls).toContain("leaveRoom");
    expect(window.location.pathname).toBe("/");
    expect(window.localStorage.getItem("belote.session.v1")).toBeNull();
    expect(await screen.findByTestId("main-menu")).toBeInTheDocument();
  });

  it("shows a room that ended while the person was away", async () => {
    const client = new FakeHubClient();
    client.rejoinResult = { ok: false, rejection: { category: "room", code: "RoomEnded" } };
    window.localStorage.setItem("belote.session.v1", JSON.stringify({ roomId: ROOM_ID, lastSeen: 1 }));
    await mount(client, `/room/${ROOM_ID}`);

    expect(await screen.findByTestId("room-unavailable")).toHaveTextContent("ended");
    expect(window.localStorage.getItem("belote.session.v1")).toBeNull();
  });
});
