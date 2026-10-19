import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E16.3: a student sits a test. Fixtures go through the API as admin: five "E2E <n>" questions (one
// per kind) and a published test for the demo student's class whose window closes 40 minutes after
// the run. Once a student starts, the API won't unpublish or delete a test, so it stays in the dev
// data but closes by itself. The questions are archived afterwards; the test keeps its copies.
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
const STUDENT = "MSS/2026/0001";
test.setTimeout(300_000);

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

test("a student sits a test: answers save, resume, takeover, hand in", async ({ page, context, request }) => {
  const admin = await api(request);
  const students = await admin.get<{ items: { classRoomId: string | null }[] }>(`/api/students?search=${encodeURIComponent(STUDENT)}`);
  const classRoomId = students.items[0]?.classRoomId;
  test.skip(!classRoomId, "the demo student has no class");
  const rooms = await admin.get<{ id: string; classLevelId: string }[]>("/api/academics/classrooms");
  const level = rooms.find((r) => r.id === classRoomId)!.classLevelId;
  const scopes = await admin.get<{ subjectId: string; classLevelId: string }[]>("/api/cbt/scopes");
  const scope = scopes.find((s) => s.classLevelId === level);
  test.skip(!scope, "no bank at the student's level");

  const base = { subjectId: scope!.subjectId, classLevelId: level, options: null, trueFalseAnswer: null, acceptedAnswers: null, markingGuide: null, topic: null, difficulty: "Easy" };
  const add = async (data: Record<string, unknown>) => {
    const r = await admin.post("/api/cbt/questions", { ...base, ...data });
    expect(r.ok()).toBeTruthy();
    const id: string = (await r.json()).id;
    questions.push(id);
    return id;
  };
  const opt = (text: string, isCorrect: boolean) => ({ id: null, text, isCorrect });
  const ids = {
    Objective: await add({ kind: "Objective", text: `${tag}: Is 2 + 2 four?`, options: [opt("Yes", true), opt("No", false)] }),
    MultiSelect: await add({ kind: "MultiSelect", text: `${tag}: Pick the even numbers.`, options: [opt("2", true), opt("3", false), opt("4", true)] }),
    TrueFalse: await add({ kind: "TrueFalse", text: `${tag}: The sun is a star.`, trueFalseAnswer: true }),
    FillInTheGap: await add({ kind: "FillInTheGap", text: `${tag}: The capital of Nigeria is ___.`, acceptedAnswers: ["Abuja"] }),
    Theory: await add({ kind: "Theory", text: `${tag}: Explain why we have day and night.`, markingGuide: "Rotation of the earth." }),
  };

  const sessions = await admin.get<{ isCurrent: boolean; terms: { id: string; isCurrent: boolean }[] }[]>("/api/academics/sessions");
  const termId = (sessions.flatMap((s) => s.terms).find((t) => t.isCurrent) ?? sessions[0].terms[0]).id;
  const now = Date.now();
  const created = await admin.post("/api/cbt/tests", {
    title: `${tag} sitting`,
    instructions: "Answer every question.",
    subjectId: scope!.subjectId,
    classLevelId: level,
    termId,
    componentId: null,
    classRoomIds: [classRoomId],
    durationMinutes: 30,
    opensAt: new Date(now - 60_000).toISOString(),
    closesAt: new Date(now + 40 * 60_000).toISOString(),
    maxAttempts: 1,
    shuffleQuestions: false,
    shuffleOptions: false,
  });
  expect(created.ok()).toBeTruthy();
  const testId: string = (await created.json()).id;
  const sections = (Object.entries(ids) as [string, string][]).map(([kind, id]) => ({ title: kind, instructions: null, kind, marksPerQuestion: 1, mode: "Fixed", questionIds: [id], randomCount: null, topic: null, difficulty: null }));
  expect((await admin.put(`/api/cbt/tests/${testId}/sections`, { sections })).ok()).toBeTruthy();
  const published = await admin.post(`/api/cbt/tests/${testId}/publish`);
  expect(published.ok(), await published.text()).toBeTruthy();

  // 1. The list and the instructions screen.
  await signIn(page, STUDENT);
  await expect(page.getByRole("link", { name: "Tests" }).first()).toBeVisible();
  await page.goto("/portal/tests");
  const card = page.getByRole("listitem").filter({ hasText: `${tag} sitting` });
  await expect(card).toContainText("Open");
  await expect(card).toContainText("30 min · 5 questions · 5 marks");
  await card.getByRole("link", { name: "Start" }).click();
  await page.waitForURL(new RegExp(`/exam/${testId}$`));
  await expect(page.getByText("Answer every question.")).toBeVisible();
  await page.getByRole("button", { name: "Start the test" }).click();
  await expect(page.getByRole("timer")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("0 of 5 answered")).toBeVisible();
  // The paper fits a phone.
  await page.setViewportSize({ width: 360, height: 760 });
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);
  await page.setViewportSize({ width: 1280, height: 800 });

  // 2. One answer per kind, whatever order the paper is in; each autosaves.
  const saved = () => expect(page.getByTestId("save-status")).toHaveText("All answers saved", { timeout: 30_000 });
  let gapNumber = 0;
  for (let n = 1; n <= 5; n++) {
    await page.getByRole("button", { name: new RegExp(`^Question ${n}, `) }).click();
    const question = page.locator("fieldset").filter({ has: page.getByText(`Question ${n}`, { exact: true }) });
    const text = await question.innerText();
    if (text.includes("Is 2 + 2 four?")) await question.getByRole("radio", { name: /Yes/ }).check();
    else if (text.includes("Pick the even numbers.")) {
      await question.getByRole("checkbox", { name: /^A\.\s*2$/ }).check();
      await question.getByRole("checkbox", { name: /^C\.\s*4$/ }).check();
    } else if (text.includes("The sun is a star.")) await question.getByRole("radio", { name: "True" }).check();
    else if (text.includes("The capital of Nigeria")) {
      gapNumber = n;
      await question.getByLabel(`Answer for the gap in question ${n}`).fill("Abuja");
    } else await question.getByLabel(`Answer for question ${n}`).fill("The earth spins.");
    await saved();
    await expect(page.getByText(`${n} of 5 answered`)).toBeVisible();
  }
  expect(gapNumber).toBeGreaterThan(0);

  // 3. A refresh resumes the same attempt with the answers.
  await page.reload();
  await page.getByRole("button", { name: "Continue" }).click();
  await expect(page.getByText("5 of 5 answered")).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: `Question ${gapNumber}, answered` }).click();
  const gapInput = page.getByLabel(`Answer for the gap in question ${gapNumber}`);
  await expect(gapInput).toHaveValue("Abuja");

  // 4. Opening it in another tab takes it over; this tab asks before taking it back.
  const other = await context.newPage();
  await other.goto(`/exam/${testId}`);
  await other.getByRole("button", { name: "Continue" }).click();
  await expect(other.getByRole("timer")).toBeVisible({ timeout: 30_000 });
  await other.close();
  await page.bringToFront();
  await gapInput.fill("abuja");
  const takeover = page.getByRole("alertdialog", { name: "This test is open somewhere else" });
  await expect(takeover).toBeVisible({ timeout: 30_000 });
  await takeover.getByRole("button", { name: "Continue here" }).click();
  await expect(takeover).toBeHidden({ timeout: 30_000 });
  await saved();

  // 5. Hand in.
  await page.getByRole("button", { name: "Hand in" }).last().click();
  const confirm = page.getByRole("alertdialog", { name: "Hand in your test?" });
  await expect(confirm).toContainText("answered every question");
  await confirm.getByRole("button", { name: "Hand in" }).click();
  await expect(page.getByRole("heading", { name: "Handed in" })).toBeVisible({ timeout: 30_000 });
  await page.getByRole("link", { name: "Back to tests" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: `${tag} sitting` })).toContainText("Handed in");
});

test("parents don't sit tests", async ({ page }) => {
  await signIn(page, "parent@maydel.test");
  await expect(page.getByRole("link", { name: "Tests" })).toHaveCount(0);
  await page.goto("/portal/tests");
  await expect(page.getByText("Tests are taken with the student's own account")).toBeVisible();
  await page.goto("/exam/00000000-0000-4000-8000-000000000000");
  await expect(page).toHaveURL(/\/portal$/);
});
