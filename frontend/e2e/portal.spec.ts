import { expect, test, type Page } from "@playwright/test";

// M1.3 portal results. Demo: parent@maydel.test (Mrs. Ada Eze) with Chidera and Kamsi Eze.
// Results only appear once an admin publishes a class; the tests handle both states.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(120_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

test("parent home shows a card per child with the latest result or the empty note", async ({ page }) => {
  await signIn(page, "parent@maydel.test");
  await expect(page).toHaveURL(/\/portal$/);
  await expect(page.getByRole("heading", { name: /Chidera Eze/ })).toBeVisible();
  await expect(page.getByRole("heading", { name: /Kamsi Eze/ })).toBeVisible();
  await expect(page.getByText(/Average \d|No results published yet/).first()).toBeVisible();
});

test("results tab: child switcher, list or empty state", async ({ page }) => {
  await signIn(page, "parent@maydel.test");
  await page.getByRole("link", { name: "Results" }).first().click();
  await page.waitForURL(/\/portal\/results/);
  const switcher = page.getByRole("navigation", { name: "Choose a child" });
  await expect(switcher).toBeVisible();
  await switcher.getByRole("link", { name: /Kamsi/ }).click();
  await expect(page).toHaveURL(/child=/);
  await expect(page.getByRole("heading", { name: /Kamsi Eze/ })).toBeVisible();
  await expect(page.getByText(/No results published yet|Term/).first()).toBeVisible();
});

test("an unpublished or someone else's report shows a friendly not-found", async ({ page }) => {
  await signIn(page, "parent@maydel.test");
  const id = "01a131b3-0000-7000-8000-000000000000";
  await page.goto(`/portal/results/${id}/${id}`);
  await expect(page.getByText("This report isn't available")).toBeVisible();
  await expect(page.getByRole("link", { name: "Back to results" })).toBeVisible();
});

test("a student sees only themselves (no switcher)", async ({ page }) => {
  await signIn(page, "MSS/2026/0001");
  await page.goto("/portal/results");
  await expect(page.getByRole("navigation", { name: "Choose a child" })).toHaveCount(0);
});

test("portal PDF and photo routes are refused for strangers", async ({ request }) => {
  const id = "01a131b3-0000-7000-8000-000000000000";
  expect((await request.get(`/api/media/portal/children/${id}/results/${id}/pdf`)).status()).toBe(401);
  expect((await request.put(`/api/media/portal/${id}/photo`, { multipart: { file: { name: "x.png", mimeType: "image/png", buffer: Buffer.from("x") } } })).status()).toBe(404);
});
