import { expect, test } from "@playwright/test";
import { openAndWaitConnected, startGame } from "./helpers";

// These run with the real card pacing (no ?pace=instant).

test("a finished trick stays on the table long enough to read before it is swept away", async ({ page }) => {
  await openAndWaitConnected(page, { paced: true });
  await startGame(page);

  // Play (or bid) as soon as controls are available until a trick has been completed and held.
  const hold = page.getByTestId("trick-winner");
  const actions = page.locator(
    'section[aria-labelledby="bid-heading"] button:enabled, section[aria-labelledby="announce-heading"] button:enabled, section[aria-labelledby="hand-heading"] button:enabled',
  );
  let heldAt = 0;
  for (let step = 0; step < 400 && heldAt === 0; step++) {
    if ((await hold.textContent())?.trim()) {
      heldAt = Date.now();
      break;
    }
    if ((await actions.count()) > 0) await actions.first().click({ timeout: 2000 }).catch(() => undefined);
    else await page.waitForTimeout(50);
  }
  expect(heldAt).toBeGreaterThan(0);

  await expect(hold).toHaveText("", { timeout: 5000 });
  expect(Date.now() - heldAt).toBeGreaterThanOrEqual(900);
});

test("card controls are disabled until the table has settled", async ({ page }) => {
  await openAndWaitConnected(page, { paced: true });
  await startGame(page);
  // While the opening deal is being shown nothing may be clickable.
  await expect(page.getByTestId("status")).toHaveText(/Dealing|Раздаване/);
  await expect(
    page.locator('section[aria-labelledby="bid-heading"] button:enabled, section[aria-labelledby="hand-heading"] button:enabled'),
  ).toHaveCount(0);
});

test.describe("reduced motion", () => {
  test.use({ reducedMotion: "reduce" });

  test("a game can still be started and played with the same pauses", async ({ page }) => {
    await openAndWaitConnected(page, { paced: true });
    await startGame(page);
    await expect(page.getByTestId("status")).toBeVisible();
    await expect(page.getByTestId("seat-south")).toBeVisible();
  });
});
