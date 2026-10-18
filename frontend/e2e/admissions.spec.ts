import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E11.4: buy the admission form. Each run registers ONE test applicant ("E2E…", e2e+…@maydel.test);
// applicant accounts can't be deleted through the API, so that test family stays. If admissions are
// closed for the session, the test opens them and puts the settings back afterwards.
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
test.setTimeout(240_000);

async function signIn(page: Page, userName: string, password = PASSWORD) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal|admissions)/);
}

async function adminApi(request: APIRequestContext) {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName: "admin@maydel.test", password: PASSWORD } });
  expect(res.ok()).toBeTruthy();
  const headers = { Authorization: `Bearer ${(await res.json()).accessToken}` };
  return {
    get: async <T>(path: string): Promise<T> => (await request.get(`${API}${path}`, { headers })).json(),
    put: (path: string, data: unknown) => request.put(`${API}${path}`, { headers, data }),
  };
}

type Settings = { sessionId: string; isOpen: boolean; formFee: number; examWindowDays: number; instructions: string | null };
let restore: Settings | null = null;

test.afterAll(async ({ request }) => {
  if (!restore) return;
  const api = await adminApi(request);
  await api.put("/api/admissions/settings", restore);
});

test("a family buys the form with a school form code", async ({ page, request }) => {
  const api = await adminApi(request);
  const sessions = await api.get<{ id: string; isCurrent: boolean }[]>("/api/academics/sessions");
  const sessionId = (sessions.find((s) => s.isCurrent) ?? sessions[0]).id;
  const settings = await api.get<Settings>(`/api/admissions/settings?sessionId=${sessionId}`);
  if (!settings.isOpen || settings.formFee <= 0) {
    restore = { sessionId, isOpen: settings.isOpen, formFee: settings.formFee, examWindowDays: settings.examWindowDays, instructions: settings.instructions };
    expect((await api.put("/api/admissions/settings", { ...restore, isOpen: true, formFee: settings.formFee > 0 ? settings.formFee : 5000 })).ok()).toBeTruthy();
  }

  const suffix = `${Date.now() % 1_000_000}`;
  const email = `e2e+${suffix}@maydel.test`;
  const password = `E2e#Apply${suffix}`;

  // 1. Register on the public page.
  await page.context().clearCookies();
  await page.goto("/admissions/apply");
  await expect(page.getByRole("heading", { name: "Apply to Maydel Spring Schools" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Download blank form" })).toBeVisible();
  await expect(async () => {
    await page.getByLabel("First name").fill("Ada");
    await expect(page.getByLabel("First name")).toHaveValue("Ada", { timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByLabel("Surname").fill(`E2E${suffix}`);
  await page.getByLabel("Class applying for").selectOption({ index: 1 });
  await page.getByLabel("Your full name").fill(`E2E Parent ${suffix}`);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Phone").fill("0803 000 0001");
  await page.getByLabel("Password", { exact: true }).fill("weak");
  await page.getByRole("button", { name: "Create account and continue" }).click();
  await expect(page.getByText("Use at least 10 characters.")).toBeVisible();
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Create account and continue" }).click();
  await page.waitForURL(/\/admissions$/, { timeout: 60_000 });
  const card = page.getByRole("listitem").filter({ hasText: `Ada E2E${suffix}` });
  await expect(card).toContainText("Form not paid yet");

  // 2. Pay online isn't configured on this machine → told to pay at the school.
  await card.getByRole("button", { name: /^Pay .* online$/ }).click();
  await expect(card.getByText(/Pay at the school and enter your form code/)).toBeVisible({ timeout: 30_000 });

  // 3. The bursar issues a code (shown once).
  await signIn(page, "bursar@maydel.test");
  await page.goto(`/admin/admissions/form-codes?sessionId=${sessionId}`);
  await expect(async () => {
    await page.getByRole("button", { name: "Issue form codes" }).click();
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  const issue = page.getByRole("dialog");
  await issue.getByLabel("Amount paid each (₦)").fill("5,000");
  await issue.getByLabel("Issued to (optional)").fill(`E2E Parent ${suffix}`);
  await issue.getByRole("button", { name: "Issue", exact: true }).click();
  const codeText = issue.locator("span.font-mono").first();
  await expect(codeText).toHaveText(/^[A-Z2-9]{4}-[A-Z2-9]{4}$/, { timeout: 30_000 });
  const code = (await codeText.innerText()).trim();
  await expect(issue.getByRole("button", { name: "Print slips" })).toBeVisible();
  await issue.getByRole("button", { name: "Done" }).click();

  // 4. The family redeems it.
  await signIn(page, email, password);
  await expect(page).toHaveURL(/\/admissions$/);
  const myCard = page.getByRole("listitem").filter({ hasText: `Ada E2E${suffix}` });
  await expect(async () => {
    await myCard.getByLabel("I have a form code").fill(code.toLowerCase().replace("-", " "));
    await myCard.getByRole("button", { name: "Use code" }).click();
    await expect(page.getByText("Form code accepted: your form is paid").first()).toBeVisible({ timeout: 10_000 });
  }).toPass({ timeout: 60_000 });
  await page.reload();
  await expect(myCard).toContainText("Form paid");

  // 5. Staff see the application; teachers can't.
  await signIn(page, "admin@maydel.test");
  await page.goto(`/admin/admissions?sessionId=${sessionId}&search=E2E${suffix}`);
  await expect(page.getByRole("row").filter({ hasText: `E2E${suffix}` })).toContainText("Form code");
  await page.goto(`/admin/settings/admissions?sessionId=${sessionId}`);
  await expect(page.getByRole("switch", { name: "Admissions open" })).toBeVisible();

  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/admissions");
  await expect(page.getByText("You don't have access to this page")).toBeVisible();

  // 6. Registering again with the same email → sign in instead.
  await page.context().clearCookies();
  await page.goto("/admissions/apply");
  await expect(async () => {
    await page.getByLabel("First name").fill("Sibling");
    await expect(page.getByLabel("First name")).toHaveValue("Sibling", { timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByLabel("Surname").fill(`E2E${suffix}`);
  await page.getByLabel("Class applying for").selectOption({ index: 1 });
  await page.getByLabel("Your full name").fill(`E2E Parent ${suffix}`);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Phone").fill("0803 000 0001");
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Create account and continue" }).click();
  await expect(page.getByText("You already have an account")).toBeVisible({ timeout: 30_000 });
});

test("applicant-only users are kept out of the console and portal", async ({ page }) => {
  await page.context().clearCookies();
  await page.goto("/admissions");
  await expect(page).toHaveURL(/\/login\?next=%2Fadmissions/);
});
