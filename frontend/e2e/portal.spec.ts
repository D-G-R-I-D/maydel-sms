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

test("parents see a child's fees with how to pay", async ({ page }) => {
  await signIn(page, "parent@maydel.test");
  await page.goto("/portal/fees");
  await expect(page.getByRole("heading", { name: "Fees", level: 1 })).toBeVisible();
  await expect(page.getByText("How to pay")).toBeVisible();
  await expect(page.getByText(/MSS\/\d{4}\/\d{4}/).first()).toBeVisible(); // the admission number to quote
  await expect(page.getByText("No fees have been billed yet").or(page.getByText(/^INV-/).first())).toBeVisible();
});

test("fee PDFs are refused for strangers and signed-out users", async ({ request }) => {
  const id = "01a131b3-b62a-776b-b7f7-3698f51d8986";
  expect((await request.get(`/api/media/finance/invoices/${id}/pdf`)).status()).toBe(401);
  expect((await request.get(`/api/media/finance/receipts/${id}/pdf`)).status()).toBe(401);
  expect((await request.get(`/api/media/portal/children/${id}/invoices/${id}/pdf`)).status()).toBe(401);
  expect((await request.get("/api/media/finance/invoices/not-a-guid/pdf")).status()).toBe(404);
});

test("pay online: without Paystack keys parents are told to pay another way", async ({ page }) => {
  // Never open a real checkout from a test: block Paystack if keys are ever configured here.
  await page.route(/paystack\.(co|com)/, (route) => route.abort());
  await signIn(page, "parent@maydel.test");
  await page.goto("/portal/fees");
  const pay = page.getByRole("button", { name: "Pay online" }).first();
  test.skip((await pay.count()) === 0, "no invoice with a balance");
  await expect(async () => {
    await pay.click();
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await expect(page.getByRole("radio", { name: /^Full balance ₦/ })).toBeChecked();
  await page.getByRole("button", { name: "Continue to Paystack" }).click();
  await expect(page.getByText("Online payment isn't available yet. Pay at the bursary or by bank transfer for now.").first()).toBeVisible({ timeout: 30_000 });
});

test("payment return page handles a missing or unknown reference", async ({ page }) => {
  await signIn(page, "parent@maydel.test");
  await page.goto("/portal/fees/paid");
  await expect(page.getByText("No payment to show")).toBeVisible();
  await page.goto("/portal/fees/paid?reference=MSS-not-a-real-ref");
  await expect(page.getByText("Payment not found")).toBeVisible();
});
