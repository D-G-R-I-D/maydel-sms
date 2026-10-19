import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E16.2 tests and exams. The admin builds a test from questions this run adds to a bank ("E2E <n>"),
// publishes, unpublishes and deletes it. Afterwards any leftover test is deleted and the questions
// archived, so the real banks and term are left as they were.
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
test.setTimeout(300_000);

const suffix = `${Date.now() % 1_000_000}`;
const tag = `E2E ${suffix}`;
const topic = `E2E-${suffix}`;

async function signIn(page: Page, userName: string) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/admin/);
}

async function api(request: APIRequestContext, userName = "admin@maydel.test") {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName, password: PASSWORD } });
  expect(res.ok()).toBeTruthy();
  const headers = { Authorization: `Bearer ${(await res.json()).accessToken}` };
  return {
    get: async <T>(path: string): Promise<T> => (await request.get(`${API}${path}`, { headers })).json(),
    post: (path: string, data: unknown = {}) => request.post(`${API}${path}`, { headers, data }),
    del: (path: string) => request.delete(`${API}${path}`, { headers }),
  };
}

type Scope = { subjectId: string; subject: string; classLevelId: string; classLevel: string };
const created: { questions: string[]; testId?: string } = { questions: [] };

test.afterAll(async ({ request }) => {
  const admin = await api(request);
  if (created.testId) {
    await admin.post(`/api/cbt/tests/${created.testId}/unpublish`);
    await admin.del(`/api/cbt/tests/${created.testId}`);
  }
  for (const id of created.questions) await admin.del(`/api/cbt/questions/${id}`);
});

test("an admin builds, publishes, unpublishes and deletes a test", async ({ page, request }) => {
  const admin = await api(request);
  const scopes = await admin.get<Scope[]>("/api/cbt/scopes");
  const rooms = await admin.get<{ id: string; name: string; classLevelId: string }[]>("/api/academics/classrooms");
  const scope = scopes.find((s) => rooms.some((r) => r.classLevelId === s.classLevelId));
  test.skip(!scope, "no bank with classes at its level");
  const arm = rooms.find((r) => r.classLevelId === scope!.classLevelId)!;

  // Bank fixtures: two objective questions for the fixed section, two true/false for the random draw.
  const base = { subjectId: scope!.subjectId, classLevelId: scope!.classLevelId, trueFalseAnswer: null, acceptedAnswers: null, markingGuide: null, difficulty: "Medium" };
  const add = async (data: Record<string, unknown>) => {
    const r = await admin.post("/api/cbt/questions", { ...base, ...data });
    expect(r.ok()).toBeTruthy();
    created.questions.push((await r.json()).id);
  };
  for (const n of [1, 2]) await add({ kind: "Objective", text: `${tag} objective ${n}`, options: [{ id: null, text: "Yes", isCorrect: true }, { id: null, text: "No", isCorrect: false }], topic: null });
  for (const n of [1, 2]) await add({ kind: "TrueFalse", text: `${tag} statement ${n}`, options: null, trueFalseAnswer: true, topic });

  // 1. Header step.
  await signIn(page, "admin@maydel.test");
  await page.getByRole("link", { name: "Tests & exams" }).first().click();
  await expect(page.getByRole("heading", { name: "Tests & exams", level: 1 })).toBeVisible();
  await page.getByRole("link", { name: "New test" }).click();
  await page.waitForURL(/\/admin\/tests\/new/);
  const title = page.getByLabel("Title");
  await expect(async () => {
    await title.fill(`${tag} test`);
    await expect(title).toHaveValue(`${tag} test`, { timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByLabel("Subject and class").selectOption({ label: `${scope!.subject} · ${scope!.classLevel}` });
  await page.getByRole("button", { name: "Create and add sections" }).click();
  await expect(page.getByText("Choose at least one class to sit the test.")).toBeVisible();
  await page.getByRole("checkbox", { name: arm.name, exact: true }).check();
  await page.getByLabel("Closing time").fill("09:20");
  await page.getByRole("button", { name: "Create and add sections" }).click();
  await expect(page.getByText(/The window is shorter than the sitting/)).toBeVisible();
  await page.getByLabel("Closing time").fill("17:00");
  const counts = page.getByLabel("Counts towards");
  const componentLabel = await counts.locator("option").nth(1).textContent();
  if (componentLabel) await counts.selectOption({ index: 1 });
  await page.getByRole("button", { name: "Create and add sections" }).click();
  await page.waitForURL(/\/admin\/tests\/[0-9a-f-]{36}$/, { timeout: 30_000 });
  created.testId = page.url().split("/").pop();
  await expect(page.getByText("Ready to publish?")).toBeVisible();
  await expect(page.getByRole("button", { name: "Publish" })).toBeDisabled();

  // 2. Sections: a fixed objective section from the picker and a random true/false draw.
  await page.getByRole("button", { name: "Add section" }).click();
  const a = page.getByRole("group", { name: "Section A" });
  await a.getByLabel("Marks per question").fill("2");
  await a.getByRole("button", { name: "Add questions" }).click();
  const picker = page.getByRole("dialog");
  await picker.getByLabel("Search the bank").fill(tag);
  await picker.getByLabel("Search the bank").press("Enter");
  await picker.getByRole("checkbox", { name: new RegExp(`${tag} objective 1`) }).check();
  await picker.getByRole("checkbox", { name: new RegExp(`${tag} objective 2`) }).check();
  await picker.getByRole("button", { name: "Add 2 questions" }).click();
  await expect(a.getByRole("list", { name: "Questions in Section A" }).getByRole("listitem")).toHaveCount(2);
  await a.getByRole("button", { name: "Move question 2 up" }).click();
  await expect(a.getByRole("listitem").first()).toContainText(`${tag} objective 2`);

  await page.getByRole("button", { name: "Add section" }).click();
  const b = page.getByRole("group", { name: "Section B" });
  await b.getByLabel("Kind").selectOption("TrueFalse");
  await b.getByText("Drawn at random").click();
  await b.getByLabel("Questions per student").fill("2");
  await b.getByLabel("Topic").selectOption(topic);
  await expect(page.getByTestId("total-marks")).toHaveText("6 marks");
  await page.getByRole("button", { name: "Save sections" }).click();
  await expect(page.getByText("Sections saved").first()).toBeVisible({ timeout: 30_000 });
  await expect(b.getByText("2 questions match in the bank.")).toBeVisible();
  if (componentLabel) await expect(page.getByTestId("scaling-line")).toContainText("6 marks →");

  // 3. Publish, unpublish, delete.
  await expect(page.getByText("Everything's in place.")).toBeVisible();
  await page.getByRole("button", { name: "Publish", exact: true }).click();
  const confirm = page.getByRole("alertdialog");
  await expect(confirm).toContainText(`Students in 1 class (${arm.name})`);
  await confirm.getByRole("button", { name: "Publish" }).click();
  await expect(page.getByRole("heading", { name: "Published", exact: true })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("button", { name: "Save sections" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Edit details" })).toHaveCount(0);
  await page.goto("/admin/tests");
  await expect(page.getByRole("link", { name: new RegExp(`${tag} test`) })).toContainText("Published");
  await page.getByRole("link", { name: new RegExp(`${tag} test`) }).click();
  await page.getByRole("button", { name: "Unpublish" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Unpublish" }).click();
  await expect(page.getByText("Ready to publish?")).toBeVisible({ timeout: 30_000 });

  await page.getByRole("button", { name: "Delete" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Delete test" }).click();
  await page.waitForURL(/\/admin\/tests(\?|$)/, { timeout: 30_000 });
  created.testId = undefined;
  await expect(page.getByRole("link", { name: new RegExp(`${tag} test`) })).toHaveCount(0);
});

test("a bursar has no tests page", async ({ page }) => {
  await signIn(page, "bursar@maydel.test");
  await expect(page.getByRole("link", { name: "Tests & exams" })).toHaveCount(0);
  await page.goto("/admin/tests");
  await expect(page.getByText("You don't have access to this page")).toBeVisible();
});
