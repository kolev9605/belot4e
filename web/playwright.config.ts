import path from "node:path";
import { defineConfig, devices } from "@playwright/test";

const dist = path.resolve(import.meta.dirname, "dist");

const port = Number(process.env.E2E_PORT ?? 5080);
// Locally the installed Chrome is used (no browser download); CI installs Playwright's Chromium.
const channel = process.env.CI ? undefined : "chrome";

// The end-to-end suite is slow (it drives real games through a browser), so it is opt-in:
// run it locally with `npm run e2e`. It never runs in CI; under CI no spec is picked up.
export default defineConfig({
  testDir: "./e2e",
  testIgnore: process.env.CI ? "**/*" : undefined,
  timeout: 120_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: { baseURL: `http://localhost:${port}`, trace: "retain-on-failure" },
  webServer: {
    command: `dotnet run --project ../src/Belote.Server --no-launch-profile --urls http://localhost:${port} -- --Table:BotDelayMs=0 --Web:RootPath=${dist}`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"], channel } },
    // The phone project only repeats the few narrow-viewport layout specs; everything else runs once, on desktop.
    {
      name: "phone",
      testMatch: /(responsive|table-polish)\.spec\.ts/,
      grep: /fit a phone|horizontal scroll|exactly one dealer marker fit the viewport/,
      use: { ...devices["Desktop Chrome"], channel, viewport: { width: 375, height: 812 }, hasTouch: true },
    },
  ],
});
