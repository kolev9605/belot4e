import path from "node:path";
import { defineConfig, devices } from "@playwright/test";

const dist = path.resolve(import.meta.dirname, "dist");

const port = 5080;
// Locally the installed Chrome is used (no browser download); CI installs Playwright's Chromium.
const channel = process.env.CI ? undefined : "chrome";

export default defineConfig({
  testDir: "./e2e",
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
    { name: "phone", use: { ...devices["Desktop Chrome"], channel, viewport: { width: 375, height: 812 }, hasTouch: true } },
  ],
});
