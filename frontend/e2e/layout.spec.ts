import { expect, test, type Page } from "@playwright/test";

// No console page may scroll sideways at phone or tablet width (user report: the class reports
// page "floated" off to the left on a phone). Wide tables scroll inside their own container.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(300_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

const pages = [
  "/admin",
  "/admin/students",
  "/admin/guardians",
  "/admin/staff",
  "/admin/academics/classes",
  "/admin/academics/subjects",
  "/admin/academics/sessions",
  "/admin/assessment",
  "/admin/reports",
  "/admin/attendance",
  "/admin/attendance?tab=summary",
  "/admin/fees",
  "/admin/invoices",
  "/admin/debtors",
  "/admin/payments",
  "/admin/settings/school",
  "/admin/settings/assessment",
  "/admin/settings/fees",
  "/admin/settings/admissions",
  "/admin/admissions",
  "/admin/admissions/form-codes",
  "/admin/admissions/exams",
  "/admin/admissions/results",
  "/admin/cbt",
  "/admin/tests",
  "/admin/tests/new",
  "/admin/audit",
];

async function noSidewaysScroll(page: Page, path: string) {
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth), { message: `${path} scrolls sideways`, timeout: 15_000 })
    .toBeLessThanOrEqual(0);
}

for (const width of [360, 768]) {
  test(`console pages fit a ${width}px screen`, async ({ page }) => {
    await page.setViewportSize({ width, height: 800 });
    await signIn(page, "admin@maydel.test");

    // The reported page: a class's reports (wide table with screen-reader-only labels).
    await page.goto("/admin/reports");
    await page.getByRole("link", { name: /Year 3 A/ }).first().click();
    await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\?termId=/);
    await expect(page.getByRole("table")).toBeVisible();
    await noSidewaysScroll(page, "class reports");
    await page.locator("tbody tr").first().getByRole("link").first().click();
    await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\/[0-9a-f-]+\?termId=/);
    await expect(page.getByRole("article")).toBeVisible();
    await noSidewaysScroll(page, "report preview");

    for (const path of pages) {
      await page.goto(path);
      await page.waitForLoadState("networkidle");
      await noSidewaysScroll(page, path);
    }
  });
}
