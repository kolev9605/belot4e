import { expect, type Page } from "@playwright/test";

const ACTIONS =
  'section[aria-labelledby="bid-heading"] button:enabled, ' +
  'section[aria-labelledby="announce-heading"] button:enabled, ' +
  'section[aria-labelledby="hand-heading"] button:enabled';

export async function openAndWaitConnected(page: Page) {
  await page.goto("/");
  await expect(page.getByTestId("connection-banner")).toHaveAttribute("data-status", "connected", { timeout: 15_000 });
}

export async function startGame(page: Page) {
  await page.getByRole("button", { name: /^(Start game|Започни игра)$/ }).click();
  await expect(page.getByTestId("status")).toBeVisible({ timeout: 15_000 });
}

/** Clicks one enabled bid, announcement or card control. Returns false when none is available. */
export async function playOneStep(page: Page): Promise<boolean> {
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
