import { defineConfig, devices } from "@playwright/test";

// Smoke tests run against a live stack: API on :5178 (docker compose) and the web app.
// Set E2E_BASE_URL to test an already-running app (e.g. the compose `web` service on :3000).
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3000";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  // One worker: every sign-in shares the API's per-IP login rate limit (10/min).
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  reporter: [["list"]],
  use: { baseURL, trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: "npm run dev",
        url: baseURL,
        reuseExistingServer: true,
        timeout: 120_000,
        env: { API_BASE_URL: process.env.API_BASE_URL ?? "http://localhost:5178" },
      },
});
