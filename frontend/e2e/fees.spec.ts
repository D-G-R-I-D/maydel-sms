import { expect, test, type Page } from "@playwright/test";

// E10 part 1: fee items, settings, invoices. Shared data stays as it was: the test fee item is deleted,
// settings are only validated (never saved), and invoices are only generated for the Creche level,
// which has no active students in the demo data.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(150_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

// Safety net: if a run dies between adding and deleting, remove "E2E fee" items through the API
// (test infrastructure only; the app itself never calls the API from the browser).
test.afterAll(async ({ request }) => {
  const API = process.env.E2E_API_URL ?? "http://localhost:5178";
  const login = await request.post(`${API}/api/auth/login`, { data: { userName: "admin@maydel.test", password: PASSWORD } });
  if (!login.ok()) return;
  const headers = { Authorization: `Bearer ${(await login.json()).accessToken}` };
  const sessions: { terms: { id: string }[] }[] = await (await request.get(`${API}/api/academics/sessions`, { headers })).json();
  for (const term of sessions.flatMap((s) => s.terms)) {
    const items: { id: string; name: string }[] = await (await request.get(`${API}/api/finance/fee-items?termId=${term.id}`, { headers })).json();
    for (const item of items.filter((i) => i.name.startsWith("E2E fee"))) await request.delete(`${API}/api/finance/fee-items/${item.id}`, { headers });
  }
});

test("add an optional fee, see who takes it, then delete it", async ({ page }) => {
  await signIn(page, "bursar@maydel.test");
  await page.goto("/admin/fees");
  await expect(page.getByRole("heading", { name: "Fee items", level: 1 })).toBeVisible();

  const name = `E2E fee ${Date.now() % 100000}`;
  await expect(async () => {
    await page.getByRole("button", { name: "Add fee" }).first().click();
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name").fill(name);
  await dialog.getByLabel("Amount (₦)").fill("12.345");
  await dialog.getByRole("switch", { name: "Mandatory" }).click(); // → optional
  await dialog.getByRole("button", { name: "Add fee" }).click();
  await expect(dialog.getByText("Enter an amount above ₦0 with at most 2 decimal places.")).toBeVisible();
  await dialog.getByLabel("Amount (₦)").fill("2,500");
  await dialog.getByRole("button", { name: "Add fee" }).click();
  await expect(page.getByText(`${name} added`)).toBeVisible({ timeout: 30_000 });

  const row = page.getByRole("main").getByRole("listitem").filter({ hasText: name });
  await expect(row).toContainText("₦2,500.00");
  await expect(row).toContainText("Optional");
  await row.getByRole("button", { name: "Who takes this?" }).click();
  const sheet = page.getByRole("dialog", { name: `Who takes ${name}?` });
  await expect(sheet.getByText(/selected|No students to choose from/).first()).toBeVisible({ timeout: 30_000 });
  await sheet.getByRole("button", { name: "Close" }).click();

  await row.getByRole("button", { name: `Delete ${name}` }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Delete fee" }).click();
  await expect(page.getByText(`${name} deleted`)).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("main").getByRole("listitem").filter({ hasText: name })).toHaveCount(0);
});

test("fee settings validate like the API; bursars see them read-only", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/fees");
  await expect(page.getByRole("heading", { name: "Sibling discount" })).toBeVisible();
  await expect(async () => {
    await page.getByRole("radio", { name: "Percentage" }).check({ force: true });
    await expect(page.getByLabel("2nd child (%)")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByLabel("2nd child (%)").fill("120");
  await expect(page.getByText("At most 100%.")).toBeVisible();
  await page.getByLabel("2nd child (%)").fill("10");
  await expect(page.getByText(/Example: child 2 pays ₦135,000\.00/)).toBeVisible();

  await page.getByRole("radio", { name: "This term" }).check({ force: true });
  await page.getByLabel("Percent", { exact: true }).first().fill("60");
  await page.getByLabel("Percent", { exact: true }).nth(1).fill("50");
  await expect(page.getByText("Parts add up to 110%; they must add up to 100%.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save instalments" })).toBeDisabled();

  await page.context().clearCookies();
  await signIn(page, "bursar@maydel.test");
  await page.goto("/admin/settings/fees");
  await expect(page.getByText("View only. Ask an administrator to change these.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save discount" })).toHaveCount(0);
});

test("invoices: list, generate for a level with no students, open one", async ({ page }) => {
  await signIn(page, "bursar@maydel.test");
  await page.goto("/admin/invoices");
  await expect(page.getByRole("heading", { name: "Invoices", level: 1 })).toBeVisible();
  await expect(page.getByRole("table").or(page.getByText(/No invoices/).first())).toBeVisible({ timeout: 30_000 });

  await expect(async () => {
    await page.getByRole("button", { name: "Generate invoices" }).click();
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Who").selectOption({ label: "Creche only" });
  await dialog.getByRole("button", { name: "Generate", exact: true }).click();
  await expect(page.getByText(/^Created \d+ · /)).toBeVisible({ timeout: 30_000 });

  const first = page.locator("tbody a").first();
  if ((await first.count()) > 0) {
    await first.click();
    await page.waitForURL(/\/admin\/invoices\/[0-9a-f-]+$/);
    await expect(page.getByRole("heading", { name: "Charges" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Payment schedule" })).toBeVisible();
  }
});

test("teachers can't open fees or invoices", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  for (const path of ["/admin/fees", "/admin/invoices", "/admin/settings/fees"]) {
    await page.goto(path);
    await expect(page.getByText("You don't have access to this page")).toBeVisible();
  }
});
