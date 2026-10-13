import { expect, test } from "@playwright/test";
import { openAndWaitConnected, playToEnd, startGame } from "./helpers";

// SC-006: across a whole game, no card reaches the browser outside South's own hand and the public table.
test("received frames never contain a card outside the viewer's hand or the public table", async ({ page }) => {
  const frames: string[] = [];
  page.on("websocket", (ws) => ws.on("framereceived", (f) => frames.push(String(f.payload))));

  await openAndWaitConnected(page);
  await startGame(page);
  await playToEnd(page);

  const CARD = /^(7|8|9|10|J|Q|K|A)[SHDC]$/;
  const updates: unknown[] = [];
  for (const frame of frames) {
    for (const part of frame.split("\u001e")) {
      if (!part.trim()) continue;
      try {
        const message = JSON.parse(part) as { type?: number; target?: string; arguments?: unknown[]; result?: unknown };
        if (message.target === "Update") updates.push(...(message.arguments ?? []));
        const result = message.result as { update?: unknown } | undefined;
        if (result?.update) updates.push(result.update);
      } catch {
        // handshake or ping frame
      }
    }
  }
  expect(updates.length).toBeGreaterThan(10);

  for (const update of updates as Array<{ view: Record<string, unknown> }>) {
    const view = update.view;
    const allowed = new Set<string>();
    const collect = (value: unknown) => {
      if (typeof value === "string" && CARD.test(value)) allowed.add(value);
      else if (Array.isArray(value)) value.forEach(collect);
      else if (value && typeof value === "object") Object.values(value).forEach(collect);
    };
    for (const key of ["ownHand", "currentTrick", "lastCompletedTrick", "completedTricks", "legalActions"]) {
      collect(view[key]);
    }

    const seen = new Set<string>();
    const scan = (value: unknown) => {
      if (typeof value === "string" && CARD.test(value)) seen.add(value);
      else if (Array.isArray(value)) value.forEach(scan);
      else if (value && typeof value === "object") Object.values(value).forEach(scan);
    };
    scan(view);
    for (const card of seen) expect(allowed.has(card)).toBe(true);

    // The view must not expose the seed or any other seat's hand under any key.
    expect(Object.keys(view)).not.toEqual(expect.arrayContaining(["seed", "hands"]));
  }
});
