import { expect, test } from "@playwright/test";
import { dismissHandResult, openAndWaitConnected, playOneStep, playToEnd, startGame } from "./helpers";

test("plays a complete game through the page and can play again", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);
  await playToEnd(page);

  const result = page.getByTestId("result");
  await expect(result).toContainText(/win|Печелят/);
  await expect(result).toContainText(/\d+ – \d+/);

  await page.getByRole("button", { name: /Play again|Играй отново/ }).click();
  await expect(page.getByTestId("result")).toBeHidden();
  await expect(page.locator('[data-testid="hand"] [data-card]').first()).toBeVisible();
});

test("the hand stays grouped by suit in a fixed order through the first two hands", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);
  const order = ["S", "H", "C", "D"];
  let hands = 0;
  for (let step = 0; step < 2000 && hands < 2; step++) {
    const suits = await page
      .getByTestId("hand")
      .locator("[data-card]")
      .evaluateAll((els) => els.map((el) => (el.getAttribute("data-card") ?? "").slice(-1)));
    const ranks = suits.map((s) => order.indexOf(s));
    expect([...ranks].sort((a, b) => a - b)).toEqual(ranks);
    if (await dismissHandResult(page)) hands++;
    else if (!(await playOneStep(page, { dismissResult: false }))) await page.waitForTimeout(20);
  }
  expect(hands).toBe(2);
});
