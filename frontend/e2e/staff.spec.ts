import { expect, test, type Page } from "@playwright/test";

// M1.1 part 2: staff & logins. Needs the API with Development demo data (Mrs. Grace Okafor).
const PASSWORD = "Demo#Pass2026";

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

test("admin lists staff and opens a profile with assignments", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/staff");
  await expect(page.getByRole("heading", { name: "Staff", exact: true })).toBeVisible();
  await page.getByRole("link", { name: /Mrs\. Grace Okafor/ }).first().click();
  await page.waitForURL(/\/admin\/staff\/[0-9a-f-]+$/);
  await expect(page.getByRole("heading", { name: /Mrs\. Grace Okafor/ })).toBeVisible();
  await expect(page.getByText("Form teacher of")).toBeVisible();
  await expect(page.getByRole("link", { name: "Year 3 A" }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Reset password" })).toBeVisible();
});

test("adding staff shows a one-time credential slip", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/staff");
  await page.getByRole("button", { name: "Add staff" }).click();
  const dialog = page.getByRole("dialog");
  const email = `e2e.${Date.now()}@maydel.test`;
  await dialog.getByLabel("Full name").fill("E2E Test Teacher");
  await dialog.getByLabel("Email").fill(email);
  await dialog.getByRole("button", { name: "Add staff member" }).click();
  await expect(dialog.getByText("This password won't be shown again.")).toBeVisible();
  await expect(dialog.getByText(email)).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Print slip" })).toBeVisible();
  // Closing forgets the password; nothing is left in browser storage.
  await dialog.getByRole("button", { name: "Done" }).click();
  await expect(dialog).toBeHidden();
  const stored = await page.evaluate(() => JSON.stringify(localStorage) + JSON.stringify(sessionStorage) + document.cookie);
  expect(stored).not.toContain(email);
});

test("staff form mirrors the API's rules", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/staff");
  await page.getByRole("button", { name: "Add staff" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Email").fill("not-an-email");
  await dialog.getByRole("button", { name: "Add staff member" }).click();
  await expect(dialog.getByText("Enter the full name.")).toBeVisible();
  await expect(dialog.getByText("Enter a valid email address.")).toBeVisible();
});

test("teacher pickers are switched on for class subjects", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/academics/classes");
  await page.getByRole("link", { name: "Year 3 A" }).first().click();
  await page.waitForURL(/\/admin\/academics\/classes\/[0-9a-f-]+$/);
  await expect(page.getByRole("combobox", { name: /Teacher for Mathematics/ })).toBeEnabled();
});

test("teachers can't open the staff list", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/staff");
  await expect(page.getByText("You don't have access to this page")).toBeVisible();
});
