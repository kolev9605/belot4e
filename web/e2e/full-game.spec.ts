import { expect, test } from "@playwright/test";
import { openAndWaitConnected, playToEnd, startGame } from "./helpers";

test("plays a complete game through the page and can play again", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);
  await playToEnd(page);

  const result = page.getByTestId("result");
  await expect(result).toContainText(/win|Печелят/);
  await expect(result).toContainText(/\d+ – \d+/);

  await page.getByRole("button", { name: /Play again|Играй отново/ }).click();
  await expect(page.getByTestId("result")).toBeHidden();
  await expect(page.getByRole("button", { name: /playable|играе/ }).first()).toBeVisible();
});
