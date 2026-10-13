import { expect, test, type Page } from "@playwright/test";
import { dismissHandResult, openAndWaitConnected, playOneStep, startGame, waitForMyTurn } from "./helpers";

// Spec 006: one-screen layout, aligned hand, neutral cards while bidding, followable bidding, result pop-up.

const HAND = '[data-testid="hand"] [data-card]';

async function verticalOverflow(page: Page) {
  return page.evaluate(() => document.documentElement.scrollHeight - window.innerHeight);
}

/** Plays (bids, then cards) until a card in the hand is a playable one, i.e. the playing phase on the person's turn. */
async function reachPlayingTurn(page: Page) {
  for (let i = 0; i < 400; i++) {
    if (await page.locator(`${HAND}[data-mode="playable"]`).count()) return;
    if (!(await playOneStep(page))) await page.waitForTimeout(25);
  }
  throw new Error("The playing phase did not start.");
}

async function expectHandFullyVisible(page: Page) {
  const viewport = page.viewportSize()!;
  const boxes = await page.locator(HAND).evaluateAll((els) =>
    els.map((el) => {
      const r = el.getBoundingClientRect();
      return { left: r.left, right: r.right, top: r.top, bottom: r.bottom };
    }),
  );
  expect(boxes.length).toBeGreaterThan(0);
  for (const box of boxes) {
    expect(box.top).toBeGreaterThanOrEqual(0);
    expect(box.bottom).toBeLessThanOrEqual(viewport.height + 1);
    expect(box.left).toBeGreaterThanOrEqual(0);
    expect(box.right).toBeLessThanOrEqual(viewport.width + 1);
  }
}

for (const size of [
  { width: 1366, height: 650 },
  { width: 1920, height: 960 },
  { width: 1280, height: 560 },
]) {
  test(`the whole table and hand fit without scrolling at ${size.width}x${size.height}`, async ({ page, isMobile }) => {
    test.skip(isMobile === true, "desktop viewports only");
    await page.setViewportSize(size);
    await openAndWaitConnected(page);
    await startGame(page);

    // Bidding.
    await waitForMyTurn(page);
    expect(await verticalOverflow(page)).toBeLessThanOrEqual(0);
    await expectHandFullyVisible(page);

    // Playing, with a trick on the table.
    await reachPlayingTurn(page);
    expect(await verticalOverflow(page)).toBeLessThanOrEqual(0);
    await expectHandFullyVisible(page);
    await playOneStep(page);
    await page.waitForTimeout(100);
    expect(await verticalOverflow(page)).toBeLessThanOrEqual(0);
    await expectHandFullyVisible(page);
  });
}

test("the hand shares one baseline with equal spacing, while bidding and while playing", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);

  const measure = async () =>
    page.locator(HAND).evaluateAll((els) =>
      els.map((el) => {
        const r = el.getBoundingClientRect();
        return { suit: (el.getAttribute("data-card") ?? "").slice(-1), left: r.left, right: r.right, top: r.top, height: r.height };
      }),
    );

  const check = async () => {
    // Only the pointed-at card may lift, so measure with the pointer away from the hand, once settled.
    await page.mouse.move(0, 0);
    await page.waitForTimeout(400);
    const cards = await measure();
    expect(cards.length).toBeGreaterThan(1);
    // One baseline: every card has the same top and height (within rounding).
    for (const c of cards) {
      expect(Math.abs(c.top - cards[0]!.top)).toBeLessThanOrEqual(0.5);
      expect(Math.abs(c.height - cards[0]!.height)).toBeLessThanOrEqual(0.5);
    }
    // Equal spacing: the left-to-left pitch is one value inside suits and one value across suit boundaries.
    const within: number[] = [];
    const across: number[] = [];
    for (let i = 1; i < cards.length; i++) {
      const pitch = cards[i]!.left - cards[i - 1]!.left;
      (cards[i]!.suit === cards[i - 1]!.suit ? within : across).push(pitch);
    }
    for (const group of [within, across]) {
      for (const pitch of group) expect(Math.abs(pitch - group[0]!)).toBeLessThanOrEqual(0.75);
    }
  };

  await waitForMyTurn(page);
  await check();
  await reachPlayingTurn(page);
  await check();
});

test("no card looks disabled while bidding; legal cards are highlighted without moving when playing", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);
  await waitForMyTurn(page);

  const modes = await page.locator(HAND).evaluateAll((els) => els.map((el) => el.getAttribute("data-mode")));
  expect(modes.length).toBeGreaterThan(0);
  expect(new Set(modes)).toEqual(new Set(["inert"]));
  await expect(page.locator(`${HAND}[data-playable="false"][data-mode="unplayable"]`)).toHaveCount(0);

  await reachPlayingTurn(page);
  const tops = await page.locator(HAND).evaluateAll((els) => els.map((el) => el.getBoundingClientRect().top));
  expect(new Set(tops.map((t) => Math.round(t))).size).toBe(1);
  await expect(page.locator(`${HAND}[data-mode="playable"]`).first()).toBeVisible();
});

test("a hand ends with a pop-up of both teams' cards, scores and totals; dismissing continues", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);

  const dialog = page.getByTestId("hand-result");
  for (let i = 0; i < 3000 && !(await dialog.isVisible()); i++) {
    if (!(await playOneStep(page, { dismissResult: false }))) await page.waitForTimeout(20);
  }
  await expect(dialog).toBeVisible();

  const ns = page.getByTestId("won-cards-northSouth");
  const ew = page.getByTestId("won-cards-eastWest");
  const counts = [await ns.locator("li").count().catch(() => 0), await ew.locator("li").count().catch(() => 0)];
  expect(counts[0]! + counts[1]!).toBe(32);

  await expect(page.getByTestId("hand-result-northSouth")).toContainText(/Game total|Общо в играта/);
  await expect(page.getByTestId("hand-result-eastWest")).toContainText(/Game points this hand|Точки за играта/);
  await expect(page.getByTestId("hand-result-outcome")).toHaveText(/Contract (made|failed)|Договорът е изпълнен|Договорът не е изпълнен|Tied|Равенство/);
  expect(await verticalOverflow(page)).toBeLessThanOrEqual(0);

  // The table behind it has not moved on to the next deal.
  const dealNumber = await page.getByText(/^(Deal|Раздаване) \d+/).first().textContent();
  await page.waitForTimeout(1500);
  await expect(dialog).toBeVisible();
  expect(await page.getByText(/^(Deal|Раздаване) \d+/).first().textContent()).toBe(dealNumber);

  expect(await dismissHandResult(page)).toBe(true);
  await expect(dialog).toBeHidden();
});

test("bot calls are paced and every seat shows its calls", async ({ page }) => {
  await openAndWaitConnected(page, { paced: true });
  await startGame(page);

  const total = () => page.locator('[data-testid^="seat-calls-"] li').count();
  const stamps: { count: number; at: number }[] = [];
  let lastClick = 0;
  let last = 0;
  const started = Date.now();
  while (Date.now() - started < 30_000) {
    const count = await total();
    if (count !== last) {
      stamps.push({ count, at: Date.now() - lastClick < 400 ? -1 : Date.now() });
      last = count;
    }
    const bid = page.locator('section[aria-labelledby="bid-heading"] button:enabled').first();
    if ((await bid.count()) > 0) {
      lastClick = Date.now();
      await bid.click({ timeout: 2000 }).catch(() => undefined);
    }
    if (stamps.filter((s) => s.at > 0).length >= 3) break;
    await page.waitForTimeout(40);
  }

  const botStamps = stamps.filter((s) => s.at > 0).map((s) => s.at);
  expect(botStamps.length).toBeGreaterThanOrEqual(2);
  for (let i = 1; i < botStamps.length; i++) {
    const gap = botStamps[i]! - botStamps[i - 1]!;
    // Consecutive bot calls are at least ~0.7 s apart (the pause may be shortened slightly when many are queued).
    if (gap < 5000) expect(gap).toBeGreaterThanOrEqual(600);
  }
  await expect(page.locator('[data-testid^="seat-calls-"]').first()).toBeVisible();
});

test.describe("phone", () => {
  test.use({ viewport: { width: 360, height: 640 } });

  test("the hand, seats and the result pop-up fit a phone without scrolling", async ({ page }) => {
    await openAndWaitConnected(page);
    await startGame(page);
    await waitForMyTurn(page);
    expect(await verticalOverflow(page)).toBeLessThanOrEqual(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);
    await expectHandFullyVisible(page);

    const dialog = page.getByTestId("hand-result");
    for (let i = 0; i < 3000 && !(await dialog.isVisible()); i++) {
      if (!(await playOneStep(page, { dismissResult: false }))) await page.waitForTimeout(20);
    }
    await expect(dialog).toBeVisible();
    const box = await dialog.boundingBox();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(361);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);
  });
});

test("the result pop-up is localized and keyboard-dismissable", async ({ page, isMobile }) => {
  test.skip(isMobile === true, "keyboard pass runs on desktop");
  await openAndWaitConnected(page);
  await page.getByRole("button", { name: /Switch language|Смени езика/ }).click();
  await startGame(page);

  const dialog = page.getByTestId("hand-result");
  for (let i = 0; i < 3000 && !(await dialog.isVisible()); i++) {
    if (!(await playOneStep(page, { dismissResult: false }))) await page.waitForTimeout(20);
  }
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText("Резултат от раздаване");
  await expect(page.getByRole("button", { name: "Продължи" })).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
});
