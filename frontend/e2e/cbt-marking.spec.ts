import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E16.4–E16.7: marking, releasing, the grade sheet preview, analytics, and released results in the
// portal. Fixtures through the API: an objective and a theory question ("E2E <n>") and a test for the
// demo student's class whose window closes ~90 s after the run starts. The student sits it through
// the API. The grade sheet is previewed only, never sent, so real scores are untouched. The released
// test stays in the dev data (it can't be deleted once sat); the questions are archived afterwards.
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
const STUDENT = "MSS/2026/0001";
test.setTimeout(420_000);

const suffix = `${Date.now() % 1_000_000}`;
const tag = `E2E ${suffix}`;

async function signIn(page: Page, userName: string) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

async function api(request: APIRequestContext, userName = "admin@maydel.test") {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName, password: PASSWORD } });
  expect(res.ok()).toBeTruthy();
  const headers = { Authorization: `Bearer ${(await res.json()).accessToken}` };
  return {
    get: async <T>(path: string): Promise<T> => (await request.get(`${API}${path}`, { headers })).json(),
    post: (path: string, data: unknown = {}) => request.post(`${API}${path}`, { headers, data }),
    put: (path: string, data: unknown) => request.put(`${API}${path}`, { headers, data }),
    del: (path: string) => request.delete(`${API}${path}`, { headers }),
  };
}

const questions: string[] = [];
test.afterAll(async ({ request }) => {
  const admin = await api(request);
  for (const id of questions) await admin.del(`/api/cbt/questions/${id}`);
});

test("a teacher marks, releases and reviews; the student and parent see the result", async ({ page, request }) => {
  const admin = await api(request);
  const students = await admin.get<{ items: { id: string; classRoomId: string | null }[] }>(`/api/students?search=${encodeURIComponent(STUDENT)}`);
  const student = students.items[0];
  test.skip(!student?.classRoomId, "the demo student has no class");
  const rooms = await admin.get<{ id: string; classLevelId: string }[]>("/api/academics/classrooms");
  const level = rooms.find((r) => r.id === student.classRoomId)!.classLevelId;
  const scopes = await admin.get<{ subjectId: string; classLevelId: string }[]>("/api/cbt/scopes");
  const scope = scopes.find((s) => s.classLevelId === level)!;
  const settings = await admin.get<{ components: { id: string; isActive: boolean }[] }>("/api/assessment/settings");
  const component = settings.components.find((c) => c.isActive);

  const base = { subjectId: scope.subjectId, classLevelId: level, options: null, trueFalseAnswer: null, acceptedAnswers: null, markingGuide: null, topic: null, difficulty: "Easy" };
  const add = async (data: Record<string, unknown>) => {
    const r = await admin.post("/api/cbt/questions", { ...base, ...data });
    expect(r.ok()).toBeTruthy();
    const id: string = (await r.json()).id;
    questions.push(id);
    return id;
  };
  const objective = await add({ kind: "Objective", text: `${tag}: Is 3 odd?`, options: [{ id: null, text: "Yes", isCorrect: true }, { id: null, text: "No", isCorrect: false }] });
  const theory = await add({ kind: "Theory", text: `${tag}: Describe the water cycle.`, markingGuide: "Evaporation, condensation, precipitation." });

  const sessions = await admin.get<{ terms: { id: string; isCurrent: boolean }[] }[]>("/api/academics/sessions");
  const termId = (sessions.flatMap((s) => s.terms).find((t) => t.isCurrent) ?? sessions[0].terms[0]).id;
  const closesAt = Date.now() + 90_000;
  const created = await admin.post("/api/cbt/tests", {
    title: `${tag} marking`,
    instructions: null,
    subjectId: scope.subjectId,
    classLevelId: level,
    termId,
    componentId: component?.id ?? null,
    classRoomIds: [student.classRoomId],
    durationMinutes: 5,
    opensAt: new Date(closesAt - 6 * 60_000).toISOString(),
    closesAt: new Date(closesAt).toISOString(),
    maxAttempts: 1,
    shuffleQuestions: false,
    shuffleOptions: false,
  });
  expect(created.ok(), await created.text()).toBeTruthy();
  const testId: string = (await created.json()).id;
  const section = (kind: string, id: string, marks: number) => ({ title: kind, instructions: null, kind, marksPerQuestion: marks, mode: "Fixed", questionIds: [id], randomCount: null, topic: null, difficulty: null });
  expect((await admin.put(`/api/cbt/tests/${testId}/sections`, { sections: [section("Objective", objective, 1), section("Theory", theory, 5)] })).ok()).toBeTruthy();
  expect((await admin.post(`/api/cbt/tests/${testId}/publish`)).ok()).toBeTruthy();

  // The student sits it through the API: the objective right, a theory answer, then hands in.
  const pupil = await api(request, STUDENT);
  const paper = await (await pupil.post(`/api/student/tests/${testId}/start`)).json();
  const items: { id: string; kind: string; options: { id: string; text: string }[] }[] = paper.items;
  const obj = items.find((i) => i.kind === "Objective")!;
  const th = items.find((i) => i.kind === "Theory")!;
  const answer = (itemId: string, body: Record<string, unknown>) => pupil.put(`/api/student/attempts/${paper.id}/items/${itemId}`, { sessionToken: paper.sessionToken, optionIds: null, trueFalse: null, text: null, ...body });
  expect((await answer(obj.id, { optionIds: [obj.options.find((o) => o.text === "Yes")!.id] })).ok()).toBeTruthy();
  expect((await answer(th.id, { text: "Water evaporates, forms clouds and falls as rain." })).ok()).toBeTruthy();
  expect((await pupil.post(`/api/student/attempts/${paper.id}/submit`, { sessionToken: paper.sessionToken })).ok()).toBeTruthy();

  // 1. Results: the row, and Release explains why it's blocked while the window is open.
  await signIn(page, "admin@maydel.test");
  await page.goto(`/admin/tests/${testId}`);
  await page.getByRole("link", { name: "Results and marking" }).click();
  await page.waitForURL(/\/results$/);
  const row = page.getByRole("row").filter({ hasText: STUDENT });
  await expect(row).toContainText("Handed in");
  await expect(row).toContainText("1 to mark");
  await expect(page.getByRole("button", { name: "Release results" })).toBeDisabled();
  await expect(page.getByText(/still open|need marking/).first()).toBeVisible();

  // 2. Mark by question (theory), then check it by student and add a comment.
  await page.getByRole("link", { name: /Describe the water cycle/ }).click();
  await page.waitForURL(/\/questions\/[0-9a-f-]+$/);
  await expect(page.getByText("Evaporation, condensation, precipitation.")).toBeVisible();
  const box = page.getByLabel(/^Mark for /).first();
  await box.fill("7");
  await box.blur();
  await expect(page.getByText("The most this question can get is 5.")).toBeVisible();
  await box.fill("4");
  await box.blur();
  await expect(page.getByText(/^Mark saved for /).first()).toBeVisible({ timeout: 30_000 });

  await page.goto(`/admin/tests/${testId}/results`);
  await expect(row).not.toContainText("to mark", { timeout: 30_000 });
  await row.getByRole("link", { name: "Review" }).click();
  await page.waitForURL(/\/attempts\/[0-9a-f-]+$/);
  await expect(page.getByTestId("attempt-score")).toHaveText("5 / 6");
  await expect(page.getByLabel("Correct option").first()).toBeVisible();
  await page.getByLabel("Comment for question 2 (optional, shown after release)").fill("Good: name condensation next time.");
  await page.getByLabel("Comment for question 2 (optional, shown after release)").blur();
  await expect(page.getByText("Mark saved for question 2").first()).toBeVisible({ timeout: 30_000 });

  // 3. After the window closes: release.
  await page.waitForTimeout(Math.max(0, closesAt - Date.now() + 5_000));
  await page.goto(`/admin/tests/${testId}/results`);
  await expect(page.getByRole("button", { name: "Release results" })).toBeEnabled();
  await page.getByRole("button", { name: "Release results" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Release" }).click();
  await expect(page.getByRole("heading", { name: "Results released" })).toBeVisible({ timeout: 30_000 });

  // 4. Analytics and the grade sheet preview (not sent).
  await expect(page.getByRole("link", { name: "Analytics" })).toBeVisible();
  await page.goto(`/admin/tests/${testId}/analytics`);
  await expect(page.getByRole("heading", { name: "Score distribution" })).toBeVisible();
  await expect(page.getByText("Describe the water cycle").first()).toBeVisible();
  if (component) {
    await expect(page.getByRole("link", { name: "Grade sheet" })).toBeVisible();
    await page.goto(`/admin/tests/${testId}/gradebook`);
    await expect(page.getByRole("row").filter({ hasText: /\d/ }).first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Send to grade sheet" })).toBeVisible();
  }

  // 5. The student sees the score and the marked paper with the comment; no answer keys.
  await signIn(page, STUDENT);
  await page.goto("/portal/tests");
  const card = page.getByRole("listitem").filter({ hasText: `${tag} marking` });
  await expect(card).toContainText("5 / 6");
  await card.getByRole("link", { name: "View marked paper" }).click();
  await expect(page.getByText("Good: name condensation next time.")).toBeVisible();
  await expect(page.getByText("Evaporation, condensation, precipitation.")).toHaveCount(0);

  // 6. A linked parent sees it under Tests.
  const parent = await api(request, "parent@maydel.test");
  const kids = await parent.get<{ studentId: string }[]>("/api/portal/children");
  if (kids.some((k) => k.studentId === student.id)) {
    await signIn(page, "parent@maydel.test");
    await page.goto(`/portal/tests?child=${student.id}`);
    await expect(page.getByRole("listitem").filter({ hasText: `${tag} marking` })).toContainText(/83.3/);
  }
});
