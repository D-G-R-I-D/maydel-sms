import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E16.8–E16.9. The dev server has no AI key, so generation itself can't run here: this checks the
// "not set up" state and the request form, and the admin settings and per-teacher allowance. The AI
// settings and the demo teacher's allowance are put back afterwards.
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
test.setTimeout(180_000);

const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC", "base64");

async function signIn(page: Page, userName: string) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/admin/);
}

async function api(request: APIRequestContext) {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName: "admin@maydel.test", password: PASSWORD } });
  const headers = { Authorization: `Bearer ${(await res.json()).accessToken}` };
  return {
    get: async <T>(path: string): Promise<T> => (await request.get(`${API}${path}`, { headers })).json(),
    put: (path: string, data: unknown) => request.put(`${API}${path}`, { headers, data }),
  };
}

type Settings = { enabled: boolean; defaultMonthlyGenerations: number; schoolMonthlyGenerations: number };
type Usage = { teachers: { userId: string; name: string; allowance: number; customAllowance: boolean }[] };
const restore: { settings?: Settings; teacher?: { userId: string; allowance: number | null } } = {};

test.afterAll(async ({ request }) => {
  const admin = await api(request);
  if (restore.settings) {
    const { enabled, defaultMonthlyGenerations, schoolMonthlyGenerations } = restore.settings;
    await admin.put("/api/cbt/ai/settings", { enabled, defaultMonthlyGenerations, schoolMonthlyGenerations });
  }
  if (restore.teacher) await admin.put(`/api/cbt/ai/allowances/${restore.teacher.userId}`, { monthlyGenerations: restore.teacher.allowance });
});

test("a teacher sees the AI form and why it's unavailable", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/cbt");
  await page.getByRole("link", { name: "Write with AI" }).click();
  await page.waitForURL(/\/admin\/cbt\/ai/);
  await expect(page.getByRole("heading", { name: "Write questions with AI", level: 1 })).toBeVisible();
  await expect(page.getByText("No student information is ever sent.")).toBeVisible();
  await expect(page.getByRole("alert").filter({ hasText: /isn't set up yet|switched off|used this month/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /^Write \d+ questions$/ })).toBeDisabled();

  // The form itself: kinds, the mix following the count, and files.
  await page.getByLabel("How many").fill("12");
  await expect(page.getByText("Adds up to 12.")).toBeVisible();
  await page.getByRole("button", { name: "More hard" }).click();
  await expect(page.getByText("These add up to 13; make them add up to 12.")).toBeVisible();
  await page.getByRole("button", { name: "Fewer hard" }).click();
  await page.getByText("Theory", { exact: true }).click();
  await page.getByTestId("ai-files").setInputFiles({ name: "notes.png", mimeType: "image/png", buffer: PNG });
  await expect(page.getByRole("list", { name: "Files to send" })).toContainText("notes.png");
  await page.getByRole("button", { name: "Remove notes.png" }).click();
  await expect(page.getByRole("list", { name: "Files to send" })).toHaveCount(0);
});

test("an admin sets AI allowances", async ({ page, request }) => {
  const admin = await api(request);
  restore.settings = await admin.get<Settings>("/api/cbt/ai/settings");
  const now = new Date(Date.now() + 3600_000);
  const usage = await admin.get<Usage>(`/api/cbt/ai/usage?year=${now.getUTCFullYear()}&month=${now.getUTCMonth() + 1}`);
  const teacher = usage.teachers.find((t) => t.name.includes("Okafor")) ?? usage.teachers[0];
  test.skip(!teacher, "no teachers");
  restore.teacher = { userId: teacher.userId, allowance: teacher.customAllowance ? teacher.allowance : null };

  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/settings/ai");
  await expect(page.getByRole("heading", { name: "AI questions", level: 1 })).toBeVisible();
  const perTeacher = page.getByLabel("Requests per teacher per month");
  await expect(async () => {
    await perTeacher.fill(String(restore.settings!.defaultMonthlyGenerations + 1));
    await expect(perTeacher).toHaveValue(String(restore.settings!.defaultMonthlyGenerations + 1), { timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByText("AI settings saved").first()).toBeVisible({ timeout: 30_000 });
  await page.reload();
  await expect(perTeacher).toHaveValue(String(restore.settings!.defaultMonthlyGenerations + 1));

  const box = page.getByLabel(`Monthly allowance for ${teacher.name}`);
  await box.fill("7");
  await box.blur();
  await expect(page.getByText(`${teacher.name} can make 7 requests a month`).first()).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("row").filter({ hasText: teacher.name })).toContainText("of 7");
  await box.fill("");
  await box.blur();
  await expect(page.getByText(`${teacher.name} is back on the default`).first()).toBeVisible({ timeout: 30_000 });
});
