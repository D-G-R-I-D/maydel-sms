import { readFileSync } from "node:fs";
import { expect, test, type Page } from "@playwright/test";

// M1.2 part 2: class reports, attendance, report preview, comments, publish guard.
// Uses Year 3 A demo data. Leaves shared data as it found it: comments are restored,
// attendance is validated but not saved, and publish is only tried when attendance is missing
// (the API then refuses with report.attendance_missing, so nothing is published).
const PASSWORD = "Demo#Pass2026";
test.setTimeout(120_000);

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

test("class reports list students with averages, attendance and status", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  await expect(page.getByRole("heading", { name: "Year 3 A reports" })).toBeVisible();
  await expect(page.getByRole("columnheader", { name: "Average" })).toBeVisible();
  await expect(page.locator("tbody tr")).toHaveCount(5);
});

test("publishing is refused (with names) while attendance is missing", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  test.skip(!(await page.getByText("Not entered").first().isVisible()), "attendance already entered; publishing would change shared data");
  await page.getByRole("button", { name: "Publish class" }).click();
  await expect(page.getByRole("alertdialog")).toContainText("will be published");
  await page.getByRole("alertdialog").getByRole("button", { name: "Publish" }).click();
  await expect(page.getByText("Attendance is missing").first()).toBeVisible({ timeout: 30_000 });
});

test("report preview shows a draft watermark and saves the principal's comment", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  await page.locator("tbody tr").first().getByRole("link").first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\/[0-9a-f-]+\?termId=/);
  await expect(page.getByRole("article")).toBeVisible();
  test.skip(await page.getByText("Published", { exact: true }).first().isVisible(), "report already published; no draft watermark and comments are locked");
  await expect(page.getByText("DRAFT – figures may change")).toBeAttached();

  const box = page.getByLabel("Principal's comment");
  const current = await box.inputValue();
  // Restore to what was there, except a comment an earlier aborted run left behind.
  const original = current.startsWith("E2E check") ? "" : current;
  const probe = `E2E check ${Date.now()}: a good term.`;
  const principalSave = page.getByRole("button", { name: "Save", exact: true }).nth(1);
  // On a slow dev server typing can land before hydration; retry until React has the edit.
  await expect(async () => {
    await box.fill(probe);
    await expect(principalSave).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await principalSave.click();
  await expect(page.getByText("Principal's comment saved")).toBeVisible({ timeout: 30_000 });

  await box.fill(original);
  await expect(principalSave).toBeEnabled();
  await principalSave.click();
  await expect(page.getByText("Principal's comment saved").last()).toBeVisible({ timeout: 30_000 });
});

test("attendance form checks days present against days opened", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await openClass(page);
  await page.getByRole("link", { name: /Attendance/ }).first().click();
  await page.waitForURL(/\/attendance\?termId=/);
  const locked = page.getByText("Every report in this class is published");
  await expect(page.getByLabel("Days school opened this term")).toBeVisible({ timeout: 30_000 });
  test.skip(await locked.isVisible(), "class already published; attendance is locked");
  await page.getByLabel("Days school opened this term").fill("60");
  await page.getByRole("button", { name: "Mark all present" }).click();
  await expect(page.getByLabel(/^Days present for/).first()).toHaveValue("60");
  await page.getByLabel(/^Days present for/).first().fill("61");
  await page.getByRole("button", { name: "Save attendance" }).click();
  await expect(page.getByText("At most 60.").first()).toBeVisible();
});

test("teachers can't write the principal's comment", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await openClass(page);
  await page.locator("tbody tr").first().getByRole("link").first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\/[0-9a-f-]+\?termId=/);
  await expect(page.getByRole("article")).toBeVisible();
  await expect(page.getByLabel("Principal's comment")).toHaveCount(0);
  // While the report is a draft the form teacher can still write theirs.
  if (!(await page.getByText("Published", { exact: true }).first().isVisible())) await expect(page.getByLabel("Form teacher's comment")).toBeEditable();
});

test("report and class PDFs download through the BFF", async ({ page }) => {
  // Playwright can't drive the native Save As dialog; without it the button falls back to a download.
  await page.addInitScript(() => {
    delete (window as { showSaveFilePicker?: unknown }).showSaveFilePicker;
  });
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  const classDownload = page.waitForEvent("download", { timeout: 60_000 });
  await page.getByRole("button", { name: "Download class PDF (print)" }).click();
  const classFile = await classDownload;
  expect(classFile.suggestedFilename()).toMatch(/\.pdf$/);
  expect(readFileSync((await classFile.path())!).subarray(0, 4).toString()).toBe("%PDF");

  await page.locator("tbody tr").first().getByRole("link").first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\/[0-9a-f-]+\?termId=/);
  const one = page.waitForEvent("download", { timeout: 60_000 });
  await page.getByRole("button", { name: "Download PDF" }).click();
  expect((await one).suggestedFilename()).toMatch(/\.pdf$/);

  // "View" serves the same PDF inline for the browser's viewer.
  const view = page.getByRole("link", { name: /^View .*opens in a new tab/ });
  await expect(view).toHaveAttribute("target", "_blank");
  const res = await page.request.get((await view.getAttribute("href"))!);
  expect(res.status()).toBe(200);
  expect(res.headers()["content-type"]).toContain("application/pdf");
  expect(res.headers()["content-disposition"]).toMatch(/^inline/);
});

test("first-term reports show no comparison", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  await page.locator("tbody tr").first().getByRole("link").first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\/[0-9a-f-]+\?termId=/);
  await expect(page.getByRole("article")).toBeVisible();
  await expect(page.getByText("Compared with earlier terms")).toHaveCount(0);
});

test("PDF routes refuse junk ids and signed-out users", async ({ request }) => {
  expect((await request.get("/api/media/reports/not-a-guid/x/pdf")).status()).toBe(404);
  const id = "01a131b3-b62a-776b-b7f7-3698f51d8986";
  expect((await request.get(`/api/media/reports/${id}/${id}/pdf`)).status()).toBe(401);
});

test("behaviour & clubs: rate a trait on a draft report and put it back", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await openClass(page);
  await expect(page.getByRole("columnheader", { name: "Behaviour" })).toBeVisible();
  await page.locator("tbody tr").first().getByRole("link").first().click();
  await page.waitForURL(/\/admin\/reports\/[0-9a-f-]+\/[0-9a-f-]+\?termId=/);
  const card = page.getByRole("heading", { name: /Behaviour & clubs/ });
  await expect(card).toBeVisible();
  await expect(page.getByText("Doesn't affect scores or position.", { exact: false })).toBeVisible();
  test.skip(await page.getByText("Published", { exact: true }).first().isVisible(), "report already published; behaviour is read-only");

  const group = page.getByRole("group").filter({ has: page.getByRole("radio") }).first();
  const radios = group.getByRole("radio");
  const checkedBefore = await radios.evaluateAll((els) => els.findIndex((e) => (e as HTMLInputElement).checked));
  const saveBehaviour = page.getByRole("button", { name: "Save behaviour & clubs" });
  const target = checkedBefore === 1 ? "Improved" : "Good"; // pick a letter that changes something
  await expect(async () => {
    await group.getByTitle(target).click();
    await expect(saveBehaviour).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await saveBehaviour.click();
  await expect(page.getByText("Behaviour & clubs saved")).toBeVisible({ timeout: 30_000 });

  // Restore the original choice.
  await radios.nth(checkedBefore).check({ force: true });
  const save = page.getByRole("button", { name: "Save behaviour & clubs" });
  if (await save.isEnabled()) await save.click();
});

test("settings: behaviour traits tab lists traits with the fixed key", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/assessment?tab=behaviour");
  await expect(page.getByRole("heading", { name: "Behaviour traits" })).toBeVisible();
  await expect(page.getByLabel("Trait name").first()).not.toHaveValue("");
  await expect(page.getByRole("list", { name: "Rating key (fixed)" })).toContainText("Exceptional");
});
