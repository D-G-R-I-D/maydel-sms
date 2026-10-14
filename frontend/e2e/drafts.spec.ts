import { expect, test, type Page } from "@playwright/test";

// Unsaved edits survive a reload (sessionStorage drafts). Uses a Year 3 A draft report's principal's
// comment and puts the original text back at the end.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(150_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

const draftKeys = (page: Page) => page.evaluate(() => Object.keys(sessionStorage).filter((k) => k.startsWith("draft:") && k.includes(":comment:principal:")));

test("a typed comment is restored after a reload and the draft is cleared on save", async ({ page }) => {
  page.on("dialog", (d) => void d.accept()); // "leave with unsaved changes?" → yes
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/reports");
  await page.getByRole("link", { name: /Year 3 A/ }).first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\?termId=/);
  await page.locator("tbody tr").first().getByRole("link").first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\/[0-9a-f-]+\?termId=/);
  const box = page.getByLabel("Principal's comment");
  test.skip((await box.count()) === 0, "report already published");

  const current = await box.inputValue();
  const original = current.startsWith("E2E draft") ? "" : current;
  const probe = `E2E draft ${Date.now()}: kept across a reload.`;
  const save = page.getByRole("button", { name: "Save", exact: true }).nth(1);
  await expect(async () => {
    await box.fill(probe);
    await expect(save).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await expect.poll(() => draftKeys(page)).toHaveLength(1); // written after the debounce

  await page.reload();
  await expect(page.getByText("Restored your unsaved changes from")).toBeVisible({ timeout: 30_000 });
  await expect(box).toHaveValue(probe);

  await save.click();
  await expect(page.getByText("Principal's comment saved")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Restored your unsaved changes from")).toBeHidden();
  await expect.poll(() => draftKeys(page)).toHaveLength(0);

  // Discard puts the saved text back and forgets the draft.
  await box.fill(`${probe} more`);
  await expect.poll(() => draftKeys(page)).toHaveLength(1);
  await page.reload();
  await page.getByRole("button", { name: "Discard" }).first().click();
  await expect(box).toHaveValue(probe);
  await expect.poll(() => draftKeys(page)).toHaveLength(0);

  // Restore the original comment.
  await expect(async () => {
    await box.fill(original);
    await expect(save).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await save.click();
  await expect(page.getByText("Principal's comment saved").last()).toBeVisible({ timeout: 30_000 });
});

test("signing out wipes drafts", async ({ page }) => {
  page.on("dialog", (d) => void d.accept());
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/school");
  const motto = page.getByLabel("Motto (optional)");
  await expect(async () => {
    await motto.fill(`E2E unsaved ${Date.now()}`);
    await expect(page.getByRole("button", { name: "Save details" })).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await expect.poll(() => page.evaluate(() => Object.keys(sessionStorage).filter((k) => k.startsWith("draft:")).length)).toBe(1);
  await page.getByRole("button", { name: /Account menu/ }).click();
  await page.getByRole("menuitem", { name: "Sign out" }).click();
  await page.waitForURL(/\/login/);
  expect(await page.evaluate(() => Object.keys(sessionStorage).filter((k) => k.startsWith("draft:")).length)).toBe(0);
});
