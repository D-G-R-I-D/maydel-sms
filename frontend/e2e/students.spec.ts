import { expect, test, type Page } from "@playwright/test";

// M1.1 part 3: students, guardians & logins. Needs the API with Development demo data
// (Year 3 A: MSS/2026/0001–0005; Mrs. Ada Eze, +2348030000000, guardian of the two Eze children).
const PASSWORD = "Demo#Pass2026";

/**
 * Test students are named "E2E …" and withdrawn at the end of each test, so they drop out of lists
 * and the portal. Guardians use a unique phone per run: never the demo parent's (+2348030000000),
 * or the API would (by design) link test students to the real demo family.
 */
const uniquePhone = () => `0809${String(Date.now()).slice(-7)}`;

async function admit(page: Page, firstName: string): Promise<string> {
  await page.goto("/admin/students/new");
  await page.getByLabel("First name").fill(firstName);
  await page.getByLabel("Surname").fill(`E2E${Date.now() % 100000}`);
  await page.getByRole("button", { name: "Admit student" }).click();
  await page.waitForURL(/\/admin\/students\/[0-9a-f-]+$/);
  return page.url();
}

async function withdraw(page: Page, profileUrl: string) {
  await page.goto(profileUrl);
  await page.getByRole("button", { name: "Change status" }).click();
  await page.getByRole("menuitem", { name: "Withdraw" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Withdraw" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Withdrawn");
}

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

test("admit students; a sibling's guardian is found by phone and reused", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  const phone = uniquePhone();

  const first = await admit(page, "Sibling One");
  await page.getByRole("button", { name: "Add guardian" }).click();
  let dialog = page.getByRole("dialog");
  await dialog.getByLabel("Phone").fill(phone);
  await dialog.getByLabel("Full name").fill("E2E Guardian");
  await dialog.getByRole("button", { name: "Add guardian" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText("E2E Guardian").first()).toBeVisible();

  const second = await admit(page, "Sibling Two");
  await page.getByRole("button", { name: "Add guardian" }).click();
  dialog = page.getByRole("dialog");
  await dialog.getByLabel("Phone").fill(phone.replace(/^0/, "+234 "));
  await expect(dialog.getByText("Existing guardian found")).toBeVisible();
  await dialog.getByLabel("Full name").fill("Different Name Typed");
  await dialog.getByRole("button", { name: "Link guardian" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText("E2E Guardian").first()).toBeVisible(); // stored name kept

  await withdraw(page, first);
  await withdraw(page, second);
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
  const profile = await admit(page, "Status Check");
  await page.getByRole("button", { name: "Change status" }).click();
  await expect(page.getByRole("menuitem", { name: "Suspend" })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: /Re-admit/ })).toHaveCount(0);
  await page.getByRole("menuitem", { name: "Suspend" }).click();
  await expect(page.getByRole("alertdialog")).toContainText("login is disabled");
  await page.getByRole("alertdialog").getByRole("button", { name: "Suspend" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Suspended");
  await withdraw(page, profile);
});

test("teachers see students read-only and can't open guardians", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/students");
  await expect(page.getByRole("link", { name: "Admit student" })).toHaveCount(0);
  await page.goto("/admin/guardians");
  await expect(page.getByText("You don't have access to this page")).toBeVisible();
});
