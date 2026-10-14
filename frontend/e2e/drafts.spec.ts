import { expect, test, type Page } from "@playwright/test";

// Unsaved edits survive a reload (sessionStorage drafts). Uses the school profile's motto, which an
// admin can always edit (report comments lock once published), and puts the original back.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(150_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

const drafts = (page: Page) => page.evaluate(() => Object.keys(sessionStorage).filter((k) => k.startsWith("draft:")));

test("unsaved edits are restored after a reload; saving or discarding clears the draft", async ({ page }) => {
  page.on("dialog", (d) => void d.accept()); // "leave with unsaved changes?" → yes
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/school");
  const motto = page.getByLabel("Motto (optional)");
  await expect(page.getByLabel("School name")).not.toHaveValue("");
  const current = await motto.inputValue();
  // A probe left by an aborted run isn't the original; fall back to the seeded motto then.
  const stripped = current.replace(/\s*E2E (draft|motto|unsaved) \d+.*$/, "");
  const original = stripped === current ? current : stripped || "The Well Spring of Knowledge";
  const save = page.getByRole("button", { name: "Save details" });
  const banner = page.getByText("Restored your unsaved changes from");

  const probe = `E2E draft ${Date.now()}`;
  await expect(async () => {
    await motto.fill(probe);
    await expect(save).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await expect.poll(() => drafts(page)).toHaveLength(1); // written after the debounce

  await page.reload();
  await expect(banner).toBeVisible({ timeout: 30_000 });
  await expect(motto).toHaveValue(probe);

  await save.click();
  await expect(page.getByText("School profile saved")).toBeVisible({ timeout: 30_000 });
  await expect(banner).toBeHidden();
  await expect.poll(() => drafts(page)).toHaveLength(0);

  // Discard puts the saved text back and forgets the draft.
  await motto.fill(`${probe} more`);
  await expect.poll(() => drafts(page)).toHaveLength(1);
  await page.reload();
  await expect(banner).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: "Discard" }).click();
  await expect(motto).toHaveValue(probe);
  await expect.poll(() => drafts(page)).toHaveLength(0);

  // Put the original motto back.
  await expect(async () => {
    await motto.fill(original);
    await expect(save).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await save.click();
  await expect(page.getByText("School profile saved").last()).toBeVisible({ timeout: 30_000 });
  await page.reload();
  await expect(motto).toHaveValue(original);
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
  await expect.poll(() => drafts(page)).toHaveLength(1);
  await page.getByRole("button", { name: /Account menu/ }).click();
  await page.getByRole("menuitem", { name: "Sign out" }).click();
  await page.waitForURL(/\/login/);
  expect(await drafts(page)).toHaveLength(0);
});
