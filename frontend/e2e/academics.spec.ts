import { expect, test, type Page } from "@playwright/test";

// M1.1 school setup: needs the API with Development demo data (2026/2027, Year 3 A, 9 subjects).
const PASSWORD = "Demo#Pass2026";

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

test("admin sees the calendar with the current term and the header chip", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/academics/sessions");
  await expect(page.getByRole("heading", { name: "Sessions & terms" })).toBeVisible();
  await expect(page.getByRole("heading", { name: /2026\/2027/ })).toBeVisible();
  await expect(page.getByText("Current").first()).toBeVisible();
  await expect(page.getByRole("link", { name: /First Term 2026\/2027/ })).toBeVisible();
  await expect(page.getByRole("button", { name: "New session" })).toBeVisible();
});

test("session form mirrors the API's rules before submitting", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/academics/sessions");
  await page.getByRole("button", { name: "New session" }).click();
  const dialog = page.getByRole("dialog", { name: "New session" });
  await dialog.getByLabel("Session").fill("2030/2032");
  await dialog.getByRole("button", { name: "Create session" }).click();
  await expect(dialog.getByText("The two years must be consecutive, e.g. 2027/2028.")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
});

test("classes are grouped by section and open their subject checklist", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/academics/classes");
  await expect(page.getByRole("heading", { name: "Primary" })).toBeVisible();
  await page.getByRole("link", { name: "Year 3 A" }).first().click();
  await page.waitForURL(/\/admin\/academics\/classes\/[0-9a-f-]+$/);
  await expect(page.getByRole("heading", { name: "Year 3 A" })).toBeVisible();
  await expect(page.getByRole("list", { name: /Subjects offered by Year 3 A/ })).toBeVisible();

  // Toggling a subject shows the unsaved-changes bar; discarding hides it again.
  const firstBox = page.getByRole("checkbox").first();
  await firstBox.click();
  await expect(page.getByText("Unsaved changes:")).toBeVisible();
  await page.getByRole("button", { name: "Discard" }).click();
  await expect(page.getByText("Unsaved changes:")).toBeHidden();
});

test("subjects catalogue is searchable", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/academics/subjects");
  await expect(page.getByRole("heading", { name: "Subjects" })).toBeVisible();
  await page.getByLabel("Search subjects").fill("zzzz-nothing");
  await expect(page.getByText("No subjects match.")).toBeVisible();
});

test("teachers see the calendar read-only", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/academics/sessions");
  await expect(page.getByText("View only.", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "New session" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Make current" })).toHaveCount(0);
});
