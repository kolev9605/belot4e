/**
 * Per-browser convenience data: the identity token, the remembered display name and the room the
 * person last entered. Every access is guarded: storage can be missing, full, or throw (private
 * windows, blocked site data), and the app must keep working without it.
 */

const IDENTITY_KEY = "belote.identity.v1";
const PROFILE_KEY = "belote.profile.v1";
const SESSION_KEY = "belote.session.v1";

export const MAX_NAME_LENGTH = 20;

const memory = new Map<string, string>();

function read(key: string): string | null {
  try {
    const value = window.localStorage.getItem(key);
    if (value !== null) return value;
  } catch {
    // fall through to the in-memory copy
  }
  return memory.get(key) ?? null;
}

function write(key: string, value: string): void {
  memory.set(key, value);
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // keep the in-memory copy only
  }
}

function remove(key: string): void {
  memory.delete(key);
  try {
    window.localStorage.removeItem(key);
  } catch {
    // nothing to do
  }
}

function randomHex(bytes: number): string {
  const buffer = new Uint8Array(bytes);
  if (typeof crypto !== "undefined" && typeof crypto.getRandomValues === "function") {
    crypto.getRandomValues(buffer);
  } else {
    for (let i = 0; i < buffer.length; i++) buffer[i] = Math.floor(Math.random() * 256);
  }
  return Array.from(buffer, (b) => b.toString(16).padStart(2, "0")).join("");
}

function parse<T>(raw: string | null): T | null {
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

/** The secret that proves "same person" to the server; created once per browser. */
export function getIdentityToken(): string {
  const saved = parse<{ token?: string }>(read(IDENTITY_KEY));
  if (saved?.token && saved.token.length >= 16) return saved.token;
  const token = randomHex(16);
  write(IDENTITY_KEY, JSON.stringify({ token }));
  return token;
}

export function loadName(): string | null {
  const name = parse<{ name?: string }>(read(PROFILE_KEY))?.name?.trim();
  return name ? name.slice(0, MAX_NAME_LENGTH) : null;
}

export function saveName(name: string): void {
  write(PROFILE_KEY, JSON.stringify({ name: name.trim().slice(0, MAX_NAME_LENGTH) }));
}

export interface SavedSession {
  roomId: string;
  lastSeen: number;
}

export function loadSession(): SavedSession | null {
  const saved = parse<Partial<SavedSession>>(read(SESSION_KEY));
  if (!saved || typeof saved.roomId !== "string" || saved.roomId.length === 0) return null;
  return { roomId: saved.roomId, lastSeen: typeof saved.lastSeen === "number" ? saved.lastSeen : 0 };
}

export function saveSession(roomId: string): void {
  write(SESSION_KEY, JSON.stringify({ roomId, lastSeen: Date.now() }));
}

export function clearSession(): void {
  remove(SESSION_KEY);
}

/** Test helper: forget the in-memory fallback so tests start clean. */
export function resetStorageForTests(): void {
  memory.clear();
}
