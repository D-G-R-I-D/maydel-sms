import { expect, test, type Page } from "@playwright/test";

// M1.2 part 1: score entry + assessment settings. Demo: Mrs. Grace Okafor grades all 9 subjects of
// Year 3 A in the current term. Tests put back whatever they change, so shared demo data stays intact.
const PASSWORD = "Demo#Pass2026";
// Grid pages compile on first hit in dev and autosave adds a debounce; allow more than the default.
test.setTimeout(120_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

async function openGrid(page: Page, subject: string) {
  await page.goto("/admin/assessment");
  await page.getByRole("link", { name: new RegExp(subject) }).first().click();
  await page.waitForURL(/\/admin\/assessment\/[0-9a-f-]+\/[0-9a-f-]+/);
  await expect(page.getByRole("table")).toBeVisible();
}

test("teacher sees their form class with all subjects", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/assessment");
  await expect(page.getByRole("heading", { name: "Year 3 A" })).toBeVisible();
  await expect(page.getByText("Form class")).toBeVisible();
  await expect(page.getByRole("link", { name: /Mathematics/ })).toBeVisible();
});

test("scores autosave, survive a reload and can be cleared", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await openGrid(page, "History");
  const cell = page.locator("tbody tr").last().locator("input").first();
  const original = await cell.inputValue();

  await cell.fill("6");
  await expect(page.getByText(/Saved \d\d:\d\d/)).toBeVisible({ timeout: 45_000 });
  await page.reload();
  await expect(page.locator("tbody tr").last().locator("input").first()).toHaveValue("6");

  // Put it back (an all-empty row deletes the entry).
  await page.locator("tbody tr").last().locator("input").first().fill(original);
  await expect(page.getByText(/Saved \d\d:\d\d/)).toBeVisible({ timeout: 45_000 });
});

test("invalid cells are flagged live and never sent", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await openGrid(page, "ICT");
  const cell = page.locator("tbody tr").first().locator("input").first();
  const original = await cell.inputValue();
  await cell.fill("999");
  await expect(cell).toHaveAttribute("aria-invalid", "true");
  await expect(page.getByText(/isn't saved: fix the red cells/)).toBeVisible({ timeout: 30_000 });
  await cell.fill(original);
});

test("Enter moves down a column; Tab moves along the row", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await openGrid(page, "French");
  const rows = page.locator("tbody tr");
  await rows.nth(0).locator("input").first().focus();
  await page.keyboard.press("Enter");
  await expect(rows.nth(1).locator("input").first()).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(rows.nth(1).locator("input").nth(1)).toBeFocused();
});

test("settings: admins edit with a live total; teachers only read", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/assessment");
  await expect(page.getByText("Total 100 / 100")).toBeVisible();
  const first = page.getByLabel(/^Max score for /).first();
  const original = await first.inputValue();
  await first.fill(String(Number(original) + 5));
  await expect(page.getByText(/Total 105 \/ 100/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Save settings" })).toBeDisabled();
  await page.getByRole("button", { name: "Undo changes" }).click();
  await expect(page.getByText("Total 100 / 100")).toBeVisible();

  await page.context().clearCookies();
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/settings/assessment");
  await expect(page.getByText("View only.", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "Save settings" })).toHaveCount(0);
});

test("grading presets fill the scale without saving", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/assessment");
  await expect(page.getByLabel("Component name").first()).not.toHaveValue("");
  await page.getByRole("button", { name: "Use a preset" }).click();
  await page.getByRole("menuitem").last().click();
  await expect(page.getByText("Applies to new and draft reports; published reports keep their figures.", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "Save settings" })).toBeEnabled();
  await page.getByRole("button", { name: "Undo changes" }).click();
  await expect(page.getByRole("button", { name: "Save settings" })).toBeDisabled();
});
