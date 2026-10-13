import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SignalRHubClient, retryDelay, type HubConnectionLike } from "../src/connection/hubClient";
import type { ConnectionStatus } from "../src/protocol/types";

class FakeConnection implements HubConnectionLike {
  state = "Disconnected";
  startAttempts = 0;
  failStarts = 0;
  private handlers = new Map<string, (...args: never[]) => void>();
  private closeHandler: (e?: Error) => void = () => {};
  private reconnectingHandler: (e?: Error) => void = () => {};
  private reconnectedHandler: (id?: string) => void = () => {};

  async start() {
    this.startAttempts += 1;
    if (this.failStarts > 0) {
      this.failStarts -= 1;
      throw new Error("refused");
    }
    this.state = "Connected";
  }
  async stop() {
    this.state = "Disconnected";
    // SignalR reports the close shortly after stop() resolves, not synchronously.
    setTimeout(() => this.closeHandler(), 50);
  }
  async invoke<T>(): Promise<T> {
    return undefined as T;
  }
  on(method: string, handler: (...args: never[]) => void) {
    this.handlers.set(method, handler);
  }
  onclose(h: (e?: Error) => void) {
    this.closeHandler = h;
  }
  onreconnecting(h: (e?: Error) => void) {
    this.reconnectingHandler = h;
  }
  onreconnected(h: (id?: string) => void) {
    this.reconnectedHandler = h;
  }

  // test controls
  drop() {
    this.state = "Reconnecting";
    this.reconnectingHandler();
  }
  recover() {
    this.state = "Connected";
    this.reconnectedHandler("new-id");
  }
  closeForGood() {
    this.state = "Disconnected";
    this.closeHandler();
  }
  emit(method: string, payload: unknown) {
    (this.handlers.get(method) as ((p: unknown) => void) | undefined)?.(payload);
  }
}

describe("restart on the same client (React StrictMode runs effects twice in development)", () => {
  it("a stale session never disturbs the new one", async () => {
    const connections: FakeConnection[] = [];
    const client = new SignalRHubClient(() => {
      const c = new FakeConnection();
      connections.push(c);
      return c;
    });
    const statuses: ConnectionStatus[] = [];
    const handlers = {
      status: (s: ConnectionStatus) => statuses.push(s),
      update: () => {},
      tableClosed: () => {},
      room: () => {},
      superseded: () => {},
    };

    client.start(handlers);
    void client.stop();
    client.start(handlers);
    await vi.advanceTimersByTimeAsync(30_000);

    expect(connections).toHaveLength(2);
    expect(connections[0]!.startAttempts).toBeLessThanOrEqual(1);
    expect(connections[1]!.startAttempts).toBe(1);
    expect(statuses.at(-1)).toBe("connected");
    expect(statuses).not.toContain("unavailable");
  });
});

function setup(failStarts = 0) {
  const connection = new FakeConnection();
  connection.failStarts = failStarts;
  const client = new SignalRHubClient(() => connection);
  const statuses: ConnectionStatus[] = [];
  const updates: unknown[] = [];
  const closed: unknown[] = [];
  client.start({
    status: (s) => statuses.push(s),
    update: (u) => updates.push(u),
    tableClosed: (m) => closed.push(m),
    room: () => {},
    superseded: () => {},
  });
  return { connection, client, statuses, updates, closed };
}

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("retry schedule", () => {
  it("is 0, 1, 2, 5 seconds and then every 10 seconds", () => {
    expect([0, 1, 2, 3, 4, 5, 50].map(retryDelay)).toEqual([0, 1000, 2000, 5000, 10000, 10000, 10000]);
  });
});

describe("initial connection", () => {
  it("connects on the first try and reports status", async () => {
    const { statuses, connection } = setup();
    await vi.advanceTimersByTimeAsync(0);
    expect(statuses).toEqual(["connecting", "connected"]);
    expect(connection.startAttempts).toBe(1);
  });

  it("keeps retrying with backoff until the server appears, without a reload", async () => {
    const { statuses, connection } = setup(6);
    await vi.advanceTimersByTimeAsync(0);
    expect(connection.startAttempts).toBe(1);
    expect(statuses.at(-1)).toBe("unavailable");

    await vi.advanceTimersByTimeAsync(1000); // attempt 2 after 1 s
    expect(connection.startAttempts).toBe(2);
    await vi.advanceTimersByTimeAsync(2000); // attempt 3 after 2 s
    expect(connection.startAttempts).toBe(3);
    await vi.advanceTimersByTimeAsync(5000); // attempt 4 after 5 s
    expect(connection.startAttempts).toBe(4);
    await vi.advanceTimersByTimeAsync(10_000); // then every 10 s
    expect(connection.startAttempts).toBe(5);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(connection.startAttempts).toBe(6);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(connection.startAttempts).toBe(7);
    expect(statuses.at(-1)).toBe("connected");
  });
});

describe("after connecting", () => {
  it("reports reconnecting then connected when the library reconnects", async () => {
    const { statuses, connection } = setup();
    await vi.advanceTimersByTimeAsync(0);
    connection.drop();
    expect(statuses.at(-1)).toBe("reconnecting");
    connection.recover();
    expect(statuses.at(-1)).toBe("connected");
  });

  it("starts over when the connection is closed for good", async () => {
    const { statuses, connection } = setup();
    await vi.advanceTimersByTimeAsync(0);
    connection.closeForGood();
    expect(statuses.at(-1)).toBe("unavailable");
    await vi.advanceTimersByTimeAsync(1000);
    expect(connection.startAttempts).toBe(2);
    expect(statuses.at(-1)).toBe("connected");
  });

  it("forwards updates and TableClosed", async () => {
    const { connection, updates, closed } = setup();
    await vi.advanceTimersByTimeAsync(0);
    connection.emit("Update", { version: 0 });
    connection.emit("TableClosed", { reason: "replaced" });
    expect(updates).toEqual([{ version: 0 }]);
    expect(closed).toEqual([{ reason: "replaced" }]);
  });

  it("stops retrying after stop()", async () => {
    const { connection, client } = setup(100);
    await vi.advanceTimersByTimeAsync(0);
    await client.stop();
    const attempts = connection.startAttempts;
    await vi.advanceTimersByTimeAsync(60_000);
    expect(connection.startAttempts).toBe(attempts);
  });
});
