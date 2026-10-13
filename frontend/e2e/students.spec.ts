import { expect, test, type Page } from "@playwright/test";

// M1.1 part 3: students, guardians & logins. Needs the API with Development demo data
// (Year 3 A: MSS/2026/0001–0005; Mrs. Ada Eze, +2348030000000, guardian of the two Eze children).
const PASSWORD = "Demo#Pass2026";

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

test("staff search students and open a profile with guardians", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/students");
  await page.getByLabel("Search students").fill("Eze");
  await expect(page).toHaveURL(/search=Eze/);
  const first = page.getByRole("link", { name: /Eze/ }).first();
  await expect(first).toBeVisible();
  await first.click();
  await page.waitForURL(/\/admin\/students\/[0-9a-f-]+$/);
  await expect(page.getByRole("heading", { name: "Guardians" })).toBeVisible();
  await expect(page.getByText("Mrs. Ada Eze").first()).toBeVisible();
  await expect(page.getByText("Primary contact").first()).toBeVisible();
});

test("admit a student, then add a guardian found by phone", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/students/new");
  await page.getByLabel("First name").fill("Zara");
  await page.getByLabel("Surname").fill(`Eze${Date.now() % 100000}`);
  await page.getByRole("button", { name: "Admit student" }).click();
  await page.waitForURL(/\/admin\/students\/[0-9a-f-]+$/);
  await expect(page.getByRole("heading", { name: /Zara/ })).toBeVisible();

  await page.getByRole("button", { name: "Add guardian" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Phone").fill("0803 000 0000");
  await expect(dialog.getByText("Existing guardian found")).toBeVisible();
  await dialog.getByLabel("Full name").fill("Ada Eze");
  await dialog.getByRole("button", { name: "Link guardian" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText("Mrs. Ada Eze").first()).toBeVisible();
});

test("issuing a student login shows a one-time slip", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/students?search=MSS%2F2026%2F0003");
  await page.locator("tbody a").first().click();
  await page.waitForURL(/\/admin\/students\/[0-9a-f-]+$/);
  await page.getByRole("button", { name: /Issue login|Reset password/ }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: /Issue login|Reset password/ }).click();
  const slip = page.getByRole("dialog", { name: "Sign-in details" });
  // The slip opens after the action's revalidation round-trip; allow for a slow dev server.
  await expect(slip.getByText("This password won't be shown again.")).toBeVisible({ timeout: 15_000 });
  await expect(slip.getByText("MSS/2026/0003", { exact: true })).toBeVisible();
});

test("status changes are confirmed and only offer allowed moves", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/students/new");
  await page.getByLabel("First name").fill("Status");
  await page.getByLabel("Surname").fill(`Check${Date.now() % 100000}`);
  await page.getByRole("button", { name: "Admit student" }).click();
  await page.waitForURL(/\/admin\/students\/[0-9a-f-]+$/);
  await page.getByRole("button", { name: "Change status" }).click();
  await expect(page.getByRole("menuitem", { name: "Suspend" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: /Re-admit/ })).toHaveCount(0);
  await page.getByRole("menuitem", { name: "Suspend" }).click();
  await expect(page.getByRole("alertdialog")).toContainText("login is disabled");
  await page.getByRole("alertdialog").getByRole("button", { name: "Suspend" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Suspended");
});

test("teachers see students read-only and can't open guardians", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/students");
  await expect(page.getByRole("link", { name: "Admit student" })).toHaveCount(0);
  await page.goto("/admin/guardians");
  await expect(page.getByText("You don't have access to this page")).toBeVisible();
});
