import { expect, test } from "@playwright/test";
import { openAndWaitConnected, playToEnd, startGame } from "./helpers";

test("no horizontal scroll through a whole game, and the language switch localizes the page", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);

  const overflow = () => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(await overflow()).toBeLessThanOrEqual(0);

  await page.getByRole("button", { name: /Switch language|Смени езика/ }).click();
  await expect(page.locator("html")).toHaveAttribute("lang", "bg");
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Белот");
  await expect(page.getByTestId("connection-banner")).toHaveText("Свързан");
  expect(await overflow()).toBeLessThanOrEqual(0);

  await playToEnd(page);
  await expect(page.getByTestId("result")).toContainText("Край на играта");
  expect(await overflow()).toBeLessThanOrEqual(0);
});

test("a full game can be driven with the keyboard alone", async ({ page, isMobile }) => {
  test.skip(isMobile === true, "keyboard pass runs on desktop");
  await openAndWaitConnected(page);
  await page.getByRole("button", { name: "Start game" }).focus();
  await page.keyboard.press("Enter");
  await expect(page.getByTestId("status")).toBeVisible();

  for (let step = 0; step < 3000; step++) {
    if (await page.getByTestId("result").isVisible()) break;
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
  await expect(page.getByTestId("result")).toBeVisible();
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
  const trick = await page.locator('section[aria-labelledby="trick-heading"]').boundingBox();
  expect(trick!.x + trick!.width).toBeLessThanOrEqual(viewport.width + 1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);
});
