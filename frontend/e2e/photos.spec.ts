import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

// Photos (ADR-006): upload through the BFF, served back privately via /api/media/…
const PASSWORD = "Demo#Pass2026";
const fixture = path.join(__dirname, "fixtures", "photo.png");

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

test("admin adds, sees and removes a student photo", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/students/new");
  await page.getByLabel("First name").fill("Photo");
  await page.getByLabel("Surname").fill(`E2E${Date.now() % 100000}`);
  await page.getByRole("button", { name: "Admit student" }).click();
  await page.waitForURL(/\/admin\/students\/[0-9a-f-]+$/);

  await page.getByRole("button", { name: "Add photo" }).click();
  const chooser = page.waitForEvent("filechooser");
  await page.getByRole("menuitem", { name: "Add photo" }).click();
  await (await chooser).setFiles(fixture);
  const dialog = page.getByRole("dialog", { name: "Add photo" });
  await expect(dialog.getByRole("img")).toBeVisible();
  await dialog.getByRole("button", { name: "Upload photo" }).click();
  await expect(dialog).toBeHidden();

  // Served through the BFF, not the API; private cache with an ETag.
  const img = page.locator('img[src^="/api/media/students/"]').first();
  await expect(img).toBeVisible();
  const src = await img.getAttribute("src");
  const res = await page.request.get(src!);
  expect(res.status()).toBe(200);
  expect(res.headers()["content-type"]).toMatch(/^image\//);
  expect(res.headers()["cache-control"]).toContain("private");
  const etag = res.headers()["etag"];
  if (etag) expect((await page.request.get(src!, { headers: { "If-None-Match": etag } })).status()).toBe(304);

  await page.getByRole("button", { name: "Change or remove photo" }).click();
  await page.getByRole("menuitem", { name: "Remove photo" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Remove photo" }).click();
  await expect(page.locator('img[src^="/api/media/students/"]')).toHaveCount(0);

  // Withdraw the test student so it drops out of lists.
  await page.getByRole("button", { name: "Change status" }).click();
  await page.getByRole("menuitem", { name: "Withdraw" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Withdraw" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Withdrawn");
});

test("the media route refuses anything but student/staff photos", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  expect((await page.request.get("/api/media/guardians/01a131b3-b62a-776b-b7f7-3698f51d8986/photo")).status()).toBe(404);
});

test("signed-out requests get no photo", async ({ request }) => {
  const res = await request.get("/api/media/students/01a131b3-b62a-776b-b7f7-3698f51d8986/photo");
  expect(res.status()).toBe(401);
});
