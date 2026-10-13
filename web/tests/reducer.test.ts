import { describe, expect, it } from "vitest";
import { initialState, reducer, type Action, type AppState } from "../src/state/reducer";
import { makeUpdate } from "./fixtures";

function run(actions: Action[], from: AppState = initialState): AppState {
  return actions.reduce(reducer, from);
}

const connected: Action = { type: "connection/status", status: "connected" };

describe("update ordering", () => {
  it("applies the first update and the next version", () => {
    const state = run([connected, { type: "view/received", update: makeUpdate(0) }, { type: "view/received", update: makeUpdate(1) }]);
    expect(state.lastVersion).toBe(1);
    expect(state.session).toBe("playing");
    expect(state.needsResync).toBe(false);
  });

  it("drops duplicate and older updates", () => {
    const base = run([connected, { type: "view/received", update: makeUpdate(3, { dealNumber: 3 }) }]);
    const state = reducer(base, { type: "view/received", update: makeUpdate(2, { dealNumber: 9 }) });
    expect(state.lastVersion).toBe(3);
    expect(state.game?.dealNumber).toBe(3);
    expect(reducer(state, { type: "view/received", update: makeUpdate(3, { dealNumber: 9 }) })).toBe(state);
  });

  it("flags a gap for resync while still showing the newer view", () => {
    const state = run([connected, { type: "view/received", update: makeUpdate(1) }, { type: "view/received", update: makeUpdate(4) }]);
    expect(state.lastVersion).toBe(4);
    expect(state.needsResync).toBe(true);
    expect(reducer(state, { type: "resync/handled" }).needsResync).toBe(false);
  });

  it("finishes the session on a finished update", () => {
    const state = run([connected, { type: "view/received", update: makeUpdate(9, { phase: "finished", legalActions: [] }, true) }]);
    expect(state.session).toBe("finished");
  });

  it("blocks input on an unsupported protocol version", () => {
    const update = { ...makeUpdate(0), protocolVersion: 99 };
    const state = run([connected, { type: "view/received", update }]);
    expect(state.blocked).toBe(true);
    expect(state.notice).toEqual({ kind: "versionMismatch" });
    expect(state.game).toBeNull();
  });
});

describe("losing a game", () => {
  const playing = run([connected, { type: "view/received", update: makeUpdate(2) }]);

  it("TableClosed loses the game and clears the view", () => {
    const state = reducer(playing, { type: "table/closed" });
    expect(state.session).toBe("lost");
    expect(state.lostReason).toBe("replaced");
    expect(state.game).toBeNull();
    expect(state.lastVersion).toBeNull();
    expect(state.pending).toBeNull();
  });

  it("a view sync with no table loses an active game", () => {
    expect(reducer(playing, { type: "sync/noTable" }).session).toBe("lost");
  });

  it("a view sync with no table leaves an idle page idle", () => {
    expect(reducer(initialState, { type: "sync/noTable" }).session).toBe("idle");
  });

  it("a finished game stays finished after a reconnect", () => {
    const done = run([connected, { type: "view/received", update: makeUpdate(9, { phase: "finished" }, true) }]);
    expect(reducer(done, { type: "sync/noTable" }).session).toBe("finished");
  });

  it("a dropped connection during play loses the game", () => {
    const state = reducer(playing, { type: "connection/status", status: "reconnecting" });
    expect(state.session).toBe("lost");
    expect(state.lostReason).toBe("dropped");
  });

  it("late pushes are ignored once lost, and a new start resets the version", () => {
    const lost = reducer(playing, { type: "table/closed" });
    expect(reducer(lost, { type: "view/received", update: makeUpdate(3) })).toBe(lost);
    const started = reducer(lost, { type: "start/begin" });
    expect(started.session).toBe("starting");
    expect(reducer(started, { type: "view/received", update: makeUpdate(0) }).lastVersion).toBe(0);
  });
});

describe("intents", () => {
  const playing = run([connected, { type: "view/received", update: makeUpdate(2) }]);
  const intent = { kind: "play", seat: "south", card: "QH" } as const;

  it("a refusal clears pending, shows the code and keeps the view", () => {
    const sent = reducer(playing, { type: "intent/sent", intent });
    const state = reducer(sent, {
      type: "intent/result",
      result: { accepted: false, rejection: { category: "illegal", code: "MustFollowSuit" } },
    });
    expect(state.pending).toBeNull();
    expect(state.notice).toEqual({ kind: "rejection", code: "MustFollowSuit" });
    expect(state.game).toBe(playing.game);
    expect(state.lastVersion).toBe(2);
  });

  it("an accepted intent stays pending until its version is applied, and the refusal then clears", () => {
    const sent = reducer(playing, { type: "intent/sent", intent });
    const accepted = reducer(sent, { type: "intent/result", result: { accepted: true, version: 3 } });
    expect(accepted.pending?.awaitingVersion).toBe(3);
    const applied = reducer(accepted, { type: "view/received", update: makeUpdate(3) });
    expect(applied.pending).toBeNull();
  });

  it("an accepted intent whose update already arrived clears immediately", () => {
    const sent = reducer(playing, { type: "intent/sent", intent });
    const ahead = reducer(sent, { type: "view/received", update: makeUpdate(3) });
    const accepted = reducer(ahead, { type: "intent/result", result: { accepted: true, version: 3 } });
    expect(accepted.pending).toBeNull();
  });

  it("a notSeated refusal loses the game", () => {
    const sent = reducer(playing, { type: "intent/sent", intent });
    const state = reducer(sent, {
      type: "intent/result",
      result: { accepted: false, rejection: { category: "notSeated", code: "NotSeated" } },
    });
    expect(state.session).toBe("lost");
  });

  it("a rejection notice clears when the next accepted state arrives", () => {
    const withNotice = reducer(playing, { type: "intent/result", result: { accepted: false, rejection: { category: "illegal", code: "X" } } });
    expect(reducer(withNotice, { type: "view/received", update: makeUpdate(3) }).notice).toBeNull();
  });
});
