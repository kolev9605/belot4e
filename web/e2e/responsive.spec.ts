import { expect, test } from "@playwright/test";
import { openAndWaitConnected, playOneStep, startGame } from "./helpers";

test("no horizontal scroll through a hand, and the language switch localizes the page", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);

  const overflow = () => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(await overflow()).toBeLessThanOrEqual(0);

  await page.getByRole("button", { name: /Switch language|Смени езика/ }).click();
  await expect(page.locator("html")).toHaveAttribute("lang", "bg");
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Белот");
  await expect(page.getByTestId("connection-banner")).toHaveText("Свързан");
  expect(await overflow()).toBeLessThanOrEqual(0);

  // One hand is enough to cover bidding, play and the result pop-up.
  const dialog = page.getByTestId("hand-result");
  for (let step = 0; step < 1500 && !(await dialog.isVisible()); step++) {
    if (!(await playOneStep(page, { dismissResult: false }))) await page.waitForTimeout(20);
  }
  await expect(dialog).toBeVisible();
  expect(await overflow()).toBeLessThanOrEqual(0);
});

test("a hand can be played with the keyboard alone", async ({ page, isMobile }) => {
  test.skip(isMobile === true, "keyboard pass runs on desktop");
  await openAndWaitConnected(page);
  await page.getByRole("button", { name: "Start game" }).focus();
  await page.keyboard.press("Enter");
  await expect(page.getByTestId("status")).toBeVisible();

  const dialog = page.getByTestId("hand-result");
  for (let step = 0; step < 1500; step++) {
    if (await dialog.isVisible()) break;
    const target = page.locator(
      'section[aria-labelledby="bid-heading"] button:enabled, section[aria-labelledby="announce-heading"] button:enabled, section[aria-labelledby="hand-heading"] button:enabled',
    );
    if ((await target.count()) === 0) {
      await page.waitForTimeout(20);
      continue;
    }
    await target.first().focus();
    await page.keyboard.press("Enter").catch(() => {});
  }
  await expect(dialog).toBeVisible();
  await expect(page.getByRole("button", { name: "Continue" })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(dialog).toBeHidden();
});

test("seats, trick area, hand and exactly one dealer marker fit the viewport", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);
  await expect(page.getByTestId("dealer-marker")).toHaveCount(1);

  const viewport = page.viewportSize()!;
  for (const id of ["seat-north", "seat-west", "seat-east", "seat-south"]) {
    const box = await page.getByTestId(id).boundingBox();
    expect(box, id).not.toBeNull();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width + 1);
  }
  // While bidding, the bid panel sits where the trick area will be.
  const trick = await page
    .locator('section[aria-labelledby="trick-heading"], section[aria-labelledby="bid-heading"]')
    .first()
    .boundingBox();
  expect(trick!.x + trick!.width).toBeLessThanOrEqual(viewport.width + 1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);
});
