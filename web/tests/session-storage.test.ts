import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearSession,
  getIdentityToken,
  loadName,
  loadSession,
  resetStorageForTests,
  saveName,
  saveSession,
} from "../src/session/storage";

beforeEach(() => {
  window.localStorage.clear();
  resetStorageForTests();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("identity and profile storage", () => {
  it("creates one stable token and keeps it", () => {
    const first = getIdentityToken();
    expect(first).toMatch(/^[0-9a-f]{32}$/);
    expect(getIdentityToken()).toBe(first);
  });

  it("remembers the name, trimmed and bounded", () => {
    expect(loadName()).toBeNull();
    saveName("  Ivo  ");
    expect(loadName()).toBe("Ivo");
    saveName("x".repeat(60));
    expect(loadName()).toHaveLength(20);
  });

  it("remembers and clears the last room", () => {
    expect(loadSession()).toBeNull();
    saveSession("room-1");
    expect(loadSession()?.roomId).toBe("room-1");
    clearSession();
    expect(loadSession()).toBeNull();
  });

  it("ignores corrupt stored values", () => {
    window.localStorage.setItem("belote.session.v1", "{not json");
    window.localStorage.setItem("belote.profile.v1", "42");
    expect(loadSession()).toBeNull();
    expect(loadName()).toBeNull();
  });

  it("keeps working when storage throws", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    const token = getIdentityToken();
    expect(token).toHaveLength(32);
    expect(getIdentityToken()).toBe(token);
    saveName("Mira");
    expect(loadName()).toBe("Mira");
    saveSession("room-9");
    expect(loadSession()?.roomId).toBe("room-9");
  });
});
