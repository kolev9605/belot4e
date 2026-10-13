import { expect, type Page } from "@playwright/test";

const ACTIONS =
  'section[aria-labelledby="bid-heading"] button:enabled, ' +
  'section[aria-labelledby="announce-heading"] button:enabled, ' +
  'section[aria-labelledby="hand-heading"] button:enabled';

/** Opens the page; pass `{ paced: true }` to keep the real card pacing (default: instant, for speed). */
export async function openAndWaitConnected(page: Page, options: { paced?: boolean } = {}) {
  await page.goto(options.paced ? "/" : "/?pace=instant");
  await expect(page.getByTestId("connection-banner")).toHaveAttribute("data-status", "connected", { timeout: 15_000 });
}

export async function startGame(page: Page) {
  await page.getByRole("button", { name: /^(Start game|Започни игра)$/ }).click();
  await expect(page.getByTestId("status")).toBeVisible({ timeout: 15_000 });
}

/**
 * Dismisses the hand-result pop-up when it is open: "Continue" after a hand, "Close" after the
 * whole game (which leaves the result panel on the table). Returns true when it dismissed one.
 */
export async function dismissHandResult(page: Page): Promise<boolean> {
  const dialog = page.getByTestId("hand-result");
  if (!(await dialog.isVisible())) return false;
  const final = (await dialog.getAttribute("data-final")) === "true";
  const button = final
    ? dialog.getByRole("button", { name: /^(Close|Затвори)$/ })
    : dialog.getByRole("button", { name: /^(Continue|Продължи)$/ });
  try {
    await button.click({ timeout: 2000 });
    return true;
  } catch {
    return false; // it closed or changed while we were clicking
  }
}

/** Clicks one enabled bid, announcement or card control. Returns false when none is available. */
export async function playOneStep(page: Page, options: { dismissResult?: boolean } = {}): Promise<boolean> {
  if (options.dismissResult !== false && (await dismissHandResult(page))) return true;
  const choice = page.locator(ACTIONS).first();
  if ((await choice.count()) === 0) return false;
  try {
    await choice.click({ timeout: 2000 });
    return true;
  } catch {
    return false; // the table moved on while we were clicking
  }
}

/** Plays through the page alone until the result panel appears. */
export async function playToEnd(page: Page, maxSteps = 3000) {
  const result = page.getByTestId("result");
  for (let step = 0; step < maxSteps; step++) {
    if (await result.isVisible()) return;
    if (!(await playOneStep(page))) await page.waitForTimeout(20);
  }
  throw new Error("The game did not finish within the step budget.");
}

/** Plays until it is the player's turn to bid or play, then returns. */
export async function waitForMyTurn(page: Page) {
  await expect(page.locator(ACTIONS).first()).toBeVisible({ timeout: 20_000 });
}
