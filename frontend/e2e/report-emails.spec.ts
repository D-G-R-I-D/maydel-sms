import { expect, test, type Page } from "@playwright/test";

// E12.3: email report cards to parents on publish. Republishes Year 3 A (already published in the
// shared data) with emails on. The dev mail provider writes .eml files locally; nothing is sent.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(180_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

async function openClass(page: Page) {
  await page.goto("/admin/reports");
  await page.getByRole("link", { name: /Year 3 A/ }).first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\?termId=/);
  await expect(page.getByRole("table")).toBeVisible();
}

test("publishing emails parents by default and lists the emails", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  await expect(page.getByRole("heading", { name: "Parent emails" })).toBeVisible();

  // Publishing republishes every scored report (already-published ones too), so it's safe on shared data.
  const publish = page.getByRole("button", { name: "Publish class", exact: true });
  await expect(publish).toBeEnabled({ timeout: 30_000 });
  await publish.click();
  const dialog = page.getByRole("alertdialog");
  const notify = dialog.getByRole("checkbox", { name: /Email each parent a copy of the report/ });
  await expect(notify).toBeChecked();
  await dialog.getByRole("button", { name: "Publish" }).click();
  await expect(page.getByText(/Published \d+ reports? · \d+ emails? queued/).first()).toBeVisible({ timeout: 60_000 });

  // The panel lists the queued emails with a status.
  await page.reload();
  await expect(page.locator("li").getByText(/^(Sending|Sent|Failed|Skipped)$/).first()).toBeVisible({ timeout: 30_000 });
});

test("guardian email fields explain report-card emails", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/guardians");
  await page.locator(`main a[href^="/admin/guardians/"]`).first().click();
  await page.waitForURL(/\/admin\/guardians\/[0-9a-f-]+$/);
  await expect(page.getByText("Used to send report cards when results are published.")).toBeVisible();
});

test("teachers don't see parent emails", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await openClass(page);
  await expect(page.getByRole("heading", { name: "Parent emails" })).toHaveCount(0);
});
