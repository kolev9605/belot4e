import { expect, test } from "@playwright/test";
import { openAndWaitConnected, startGame, waitForMyTurn } from "./helpers";

test("unplayable cards cannot be played and a legal card plays exactly once", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);

  // Reach the playing phase by taking the first legal bid action until cards become playable.
  const hand = page.locator('section[aria-labelledby="hand-heading"] button');
  for (let i = 0; i < 200; i++) {
    await waitForMyTurn(page);
    if ((await page.locator('section[aria-labelledby="bid-heading"]').count()) === 0) break;
    await page
      .locator('section[aria-labelledby="bid-heading"] button:enabled')
      .first()
      .click({ timeout: 2000 })
      .catch(() => {});
  }

  await waitForMyTurn(page);
  const before = await hand.count();
  const disabled = page.locator('section[aria-labelledby="hand-heading"] button:disabled');
  if ((await disabled.count()) > 0) {
    await disabled.first().click({ force: true }).catch(() => {});
    await expect(hand).toHaveCount(before);
  }

  const playable = page.locator('section[aria-labelledby="hand-heading"] button:enabled').first();
  await playable.dblclick({ timeout: 2000 }).catch(() => {});
  await expect(hand).toHaveCount(before - 1, { timeout: 10_000 });
});
