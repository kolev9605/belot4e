import { expect, test } from "@playwright/test";
import { openAndWaitConnected, startGame, waitForMyTurn } from "./helpers";

test("a refresh mid-game never blocks starting a new game", async ({ page }) => {
  await openAndWaitConnected(page);
  await startGame(page);
  await waitForMyTurn(page);

  await page.reload();
  await expect(page.getByTestId("connection-banner")).toHaveAttribute("data-status", "connected", { timeout: 15_000 });
  await startGame(page);
  await waitForMyTurn(page);
});

test("a game started in a second tab closes the first tab's game", async ({ browser }) => {
  const context = await browser.newContext();
  const first = await context.newPage();
  await openAndWaitConnected(first);
  await startGame(first);
  await waitForMyTurn(first);

  const second = await context.newPage();
  await openAndWaitConnected(second);
  await startGame(second);
  await waitForMyTurn(second);

  await expect(first.getByTestId("game-lost")).toBeVisible({ timeout: 10_000 });
  await expect(first.getByRole("button", { name: /Start game|Започни игра/ })).toBeEnabled();
  await context.close();
});

test("going offline shows the loss and recovery returns to a startable state without reload", async ({ page, context }) => {
  test.setTimeout(180_000);
  await openAndWaitConnected(page);
  await startGame(page);
  await waitForMyTurn(page);

  // The browser notices a dead socket when keep-alives stop arriving (about 30 s).
  await context.setOffline(true);
  await expect(page.getByTestId("connection-banner")).not.toHaveAttribute("data-status", "connected", {
    timeout: 60_000,
  });
  await expect(page.getByTestId("game-lost")).toBeVisible();
  await expect(page.getByRole("button", { name: /Start game|Започни игра/ })).toBeDisabled();

  await context.setOffline(false);
  await expect(page.getByTestId("connection-banner")).toHaveAttribute("data-status", "connected", { timeout: 60_000 });
  await expect(page.getByRole("button", { name: /Start game|Започни игра/ })).toBeEnabled();

  await startGame(page);
  await waitForMyTurn(page);
});
