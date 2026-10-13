import { useEffect, useState } from "react";

const GUID = /[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}/i;

/** Formats 32 hex digits as a dashed GUID; leaves an already dashed one alone. */
function dashed(hex: string): string {
  const h = hex.replace(/-/g, "").toLowerCase();
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

/**
 * Pulls a room id out of what a person typed or pasted: a bare GUID (dashes optional, braces ignored)
 * or any text containing `/room/<guid>`. Returns null when there is nothing that looks like a room.
 */
export function parseRoomLink(input: string | null | undefined): string | null {
  const text = (input ?? "").trim().replace(/^[{(\s]+|[})\s]+$/g, "");
  if (!text) return null;
  const marked = /\/room\/([^/?#\s]+)/i.exec(text);
  const candidate = marked ? marked[1]! : text;
  const match = GUID.exec(candidate);
  if (!match || match[0].length !== candidate.replace(/^[{(]+|[})]+$/g, "").length) {
    // The candidate must be the GUID itself, not merely contain one.
    if (!marked || !match) return null;
  }
  return match ? dashed(match[0]) : null;
}

export type Route = { kind: "menu" } | { kind: "room"; roomId: string };

export function routeFromPath(pathname: string): Route {
  const match = /^\/room\/([^/?#]+)\/?$/i.exec(pathname);
  if (match) {
    const id = parseRoomLink(match[1]);
    if (id) return { kind: "room", roomId: id };
  }
  return { kind: "menu" };
}

export function roomPath(roomId: string): string {
  return `/room/${roomId}`;
}

/** The full shareable link for a room on this site. */
export function roomUrl(roomId: string): string {
  return `${window.location.origin}${roomPath(roomId)}`;
}

const listeners = new Set<() => void>();

export function navigate(path: string, replace = false): void {
  try {
    if (replace) window.history.replaceState(null, "", path);
    else window.history.pushState(null, "", path);
  } catch {
    // history can be unavailable (sandboxed frames); the in-memory route below still updates
  }
  listeners.forEach((l) => l());
}

export function useRoute(): Route {
  const [route, setRoute] = useState<Route>(() => routeFromPath(window.location.pathname));
  useEffect(() => {
    const update = () => setRoute(routeFromPath(window.location.pathname));
    listeners.add(update);
    window.addEventListener("popstate", update);
    return () => {
      listeners.delete(update);
      window.removeEventListener("popstate", update);
    };
  }, []);
  return route;
}
