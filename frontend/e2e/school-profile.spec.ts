import { expect, test, type Page } from "@playwright/test";

// E2.6 school profile. Leaves shared data as it found it: the motto is restored, and images are
// only uploaded (then removed) when none was there before.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(120_000);

// 8×8 red PNG; the API re-encodes whatever it gets.
const PNG = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC",
  "base64",
);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

test("admin edits the details and the change is saved", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/school");
  const motto = page.getByLabel("Motto (optional)");
  await expect(page.getByLabel("School name")).not.toHaveValue("");
  const current = await motto.inputValue();
  const original = current.replace(/\s*E2E motto \d+$/, ""); // drop a probe an aborted run left behind
  const save = page.getByRole("button", { name: "Save details" });

  // Client validation mirrors the API.
  await expect(async () => {
    await page.getByLabel("Email (optional)").fill("not-an-email");
    await expect(save).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await save.click();
  await expect(page.getByText("Enter a valid email address.")).toBeVisible();
  await page.reload();

  const probe = `E2E motto ${Date.now()}`;
  await expect(async () => {
    await motto.fill(probe);
    await expect(motto).toHaveValue(probe, { timeout: 2_000 });
    await expect(save).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await save.click();
  await expect(page.getByText("School profile saved")).toBeVisible({ timeout: 30_000 });
  await page.reload();
  await expect(motto).toHaveValue(probe);

  await expect(async () => {
    await motto.fill(original);
    await expect(motto).toHaveValue(original, { timeout: 2_000 });
    await expect(save).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await save.click();
  await expect(page.getByText("School profile saved").last()).toBeVisible({ timeout: 30_000 });
});

for (const asset of ["logo", "signature"] as const) {
  test(`admin uploads and removes the ${asset}`, async ({ page }) => {
    await signIn(page, "admin@maydel.test");
    await page.goto("/admin/settings/school");
    test.skip(await page.getByRole("img", { name: `Current ${asset}` }).isVisible(), `a ${asset} is already uploaded; leaving it alone`);

    await expect(async () => {
      await page.locator('input[type="file"]').nth(asset === "logo" ? 0 : 1).setInputFiles({ name: `${asset}.png`, mimeType: "image/png", buffer: PNG });
      await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    await page.getByRole("dialog").getByRole("button", { name: `Upload ${asset}` }).click();
    const current = page.getByRole("img", { name: `Current ${asset}` });
    await expect(current).toBeVisible({ timeout: 30_000 });
    await expect.poll(() => current.evaluate((el) => (el as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);

    if (asset === "logo") {
      expect((await page.request.get("/api/media/school/logo")).status()).toBe(200);
    }

    await page.getByRole("button", { name: "Remove" }).nth(0).click();
    await page.getByRole("alertdialog").getByRole("button", { name: `Remove ${asset}` }).click();
    await expect(current).toBeHidden({ timeout: 30_000 });
  });
}

test("teachers can't open the school profile; signature isn't public", async ({ page, request }) => {
  expect((await request.get("/api/media/school/signature")).status()).toBe(401);
  expect((await request.get("/api/media/school/banner")).status()).toBe(404);
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/settings/school");
  await expect(page.getByText("You don't have access to this page")).toBeVisible();
  expect((await page.request.get("/api/media/school/signature")).status()).toBe(403);
});
