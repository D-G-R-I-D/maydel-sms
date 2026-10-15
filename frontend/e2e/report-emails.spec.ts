import { expect, test, type Page } from "@playwright/test";

// E12.3: email report cards to parents on publish. Publishing only touches unpublished reports, so with
// Year 3 A fully published the test reopens ONE report and publishes it again, ending fully published.
// The dev mail provider writes .eml files locally; nothing is sent.
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

test("publish only sends what isn't published yet, and emails those parents", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  await expect(page.getByRole("heading", { name: "Parent emails" })).toBeVisible();

  // A fully published class shows a status instead of a Publish button.
  const allPublished = page.getByRole("status").filter({ hasText: /^All \d+ reports? published/ });
  if (await allPublished.isVisible()) {
    await expect(page.getByRole("button", { name: /^Publish/ })).toHaveCount(0);
    // Reopen one report so there's something to publish.
    const first = page.getByRole("checkbox", { name: /^Select (?!all)/ }).first();
    await expect(async () => {
      await first.check();
      await expect(page.getByRole("button", { name: /Unpublish selected \(1\)/ })).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    await page.getByRole("button", { name: /Unpublish selected \(1\)/ }).click();
    await page.getByRole("alertdialog").getByRole("button", { name: "Unpublish" }).click();
  }

  const publish = page.getByRole("button", { name: /^Publish (\d+ remaining|class)$/ });
  await expect(publish).toBeEnabled({ timeout: 60_000 });
  await publish.click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog.getByRole("checkbox", { name: /Email each parent a copy of the report/ })).toBeChecked();
  await expect(dialog).toContainText("will be emailed");
  await dialog.getByRole("button", { name: "Publish" }).click();
  await expect(page.getByText(/^Published \d+ reports?( · \d+ already published)? · \d+ emails? queued$/).first()).toBeVisible({ timeout: 60_000 });

  // Back to fully published; the panel lists the emails with a status.
  await page.reload();
  await expect(allPublished).toBeVisible({ timeout: 60_000 });
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
