import { expect, test, type Page } from "@playwright/test";

// E0.2 smoke: needs the API with Development demo data (docker compose up) and the web app.
const PASSWORD = "Demo#Pass2026";

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
}

test("admin signs in, sees the console shell and opens the audit log", async ({ page, context }) => {
  await signIn(page, "admin@maydel.test");
  await expect(page).toHaveURL(/\/admin$/);
  await expect(page.getByRole("heading", { level: 1 })).toContainText(/Good (morning|afternoon|evening)/);
  await expect(page.getByText(/Welcome back/)).toBeVisible();

  // Tokens stay in httpOnly cookies, never readable by page JS.
  const cookies = await context.cookies();
  expect(cookies.find((c) => c.name === "access_token")?.httpOnly).toBe(true);
  expect(cookies.find((c) => c.name === "refresh_token")?.httpOnly).toBe(true);
  expect(await page.evaluate(() => document.cookie)).not.toContain("access_token");

  await page.getByRole("link", { name: "Audit log" }).first().click();
  await expect(page).toHaveURL(/\/admin\/audit/);
  await expect(page.getByRole("heading", { name: "Audit log" })).toBeVisible();
  await expect(page.getByLabel("Record type")).toBeVisible();
  await expect(page.getByRole("table").or(page.getByText(/Nothing has been recorded yet|No changes match/)).first()).toBeVisible();
});

test("a parent is kept out of the staff console", async ({ page }) => {
  await signIn(page, "parent@maydel.test");
  await expect(page).toHaveURL(/\/portal$/);
  await page.goto("/admin/audit");
  await expect(page).toHaveURL(/\/portal$/);
});

test("signed-out visitors are sent to sign in and come back afterwards", async ({ page }) => {
  await page.goto("/admin/audit");
  await expect(page).toHaveURL(/\/login\?next=%2Fadmin%2Faudit/);
});

test("wrong password shows the API's message on the form", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill("nobody@maydel.test"); // unknown user: never locks out a real demo account
  await page.getByLabel("Password", { exact: true }).fill("not-the-password");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Couldn't sign you in" })).toBeVisible();
});

test("a user with a temporary password is forced to change it", async ({ page }) => {
  await signIn(page, "newteacher@maydel.test");
  await expect(page).toHaveURL(/\/change-password$/);
  await expect(page.getByRole("heading", { name: "Set a new password" })).toBeVisible();

  // Every other page bounces back until the password is changed.
  await page.goto("/admin");
  await expect(page).toHaveURL(/\/change-password$/);

  // Client-side rules mirror the API.
  await page.getByLabel("Current password").fill(PASSWORD);
  await page.getByLabel("New password", { exact: true }).fill("short");
  await page.getByLabel("Confirm new password").fill("short");
  await page.getByRole("button", { name: "Change password" }).click();
  await expect(page.getByText("Use at least 10 characters.")).toBeVisible();
});
