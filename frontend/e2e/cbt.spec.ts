import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E16.1 question bank. Every question this test writes starts with "E2E <suffix>" and is archived
// afterwards (archive is the API's delete), so the real banks are left as they were.
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
test.setTimeout(240_000);

const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC", "base64");
const suffix = `${Date.now() % 1_000_000}`;
const tag = `E2E ${suffix}`;

async function signIn(page: Page, userName: string) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/admin/);
}

async function api(request: APIRequestContext, userName: string) {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName, password: PASSWORD } });
  expect(res.ok()).toBeTruthy();
  const headers = { Authorization: `Bearer ${(await res.json()).accessToken}` };
  return {
    get: async <T>(path: string): Promise<T> => (await request.get(`${API}${path}`, { headers })).json(),
    del: (path: string) => request.delete(`${API}${path}`, { headers }),
  };
}

type Scope = { subjectId: string; subject: string; classLevelId: string; classLevel: string };
let scope: Scope | undefined;

test.afterAll(async ({ request }) => {
  if (!scope) return;
  const admin = await api(request, "admin@maydel.test");
  const q = new URLSearchParams({ subjectId: scope.subjectId, classLevelId: scope.classLevelId, search: tag, pageSize: "50" });
  const left = await admin.get<{ items: { id: string }[] }>(`/api/cbt/questions?${q.toString()}`);
  for (const item of left.items) await admin.del(`/api/cbt/questions/${item.id}`);
});

test("a teacher builds, previews, edits and archives questions", async ({ page, request }) => {
  const teacher = await api(request, "teacher@maydel.test");
  const scopes = await teacher.get<Scope[]>("/api/cbt/scopes");
  test.skip(scopes.length === 0, "the demo teacher has no question banks");
  scope = scopes[0];

  await signIn(page, "teacher@maydel.test");
  await page.getByRole("link", { name: "Question bank" }).first().click();
  await expect(page.getByRole("heading", { name: "Question bank", level: 1 })).toBeVisible();
  await page.getByRole("region", { name: scope.classLevel }).getByRole("link", { name: new RegExp(scope.subject) }).click();
  await expect(page.getByRole("heading", { name: `${scope.subject} · ${scope.classLevel}` })).toBeVisible();
  await page.getByRole("link", { name: "Add question" }).first().click();
  await page.waitForURL(/\/admin\/cbt\/questions\/new/);

  // 1. Objective: client rules first (no correct option), then save and add another.
  const text = page.getByLabel("Question", { exact: true });
  await expect(async () => {
    await text.fill(`${tag}: 2 + 2 = ?`);
    await expect(text).toHaveValue(`${tag}: 2 + 2 = ?`, { timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  for (const [i, v] of ["3", "4", "5", "22"].entries()) await page.getByLabel(`Option ${String.fromCharCode(65 + i)}`, { exact: true }).fill(v);
  await page.getByRole("button", { name: "Save and add another" }).click();
  await expect(page.getByText("Mark exactly one option as correct.")).toBeVisible();
  await page.getByLabel("Option B is correct").check();
  await page.getByLabel("Topic (optional)").fill("E2E arithmetic");
  await page.getByRole("button", { name: "Remove option D" }).click();
  await page.getByRole("button", { name: "Save and add another" }).click();
  await expect(page.getByText("Question added").first()).toBeVisible({ timeout: 30_000 });
  await expect(text).toHaveValue("");
  await expect(page.getByLabel("Topic (optional)")).toHaveValue("E2E arithmetic");

  // 2. Theory needs a marking guide.
  await page.getByLabel("Kind").selectOption("Theory");
  await text.fill(`${tag}: Explain why the sky looks blue.`);
  await page.getByRole("button", { name: "Save and add another" }).click();
  await expect(page.getByText("Write the marking guide for this question.")).toBeVisible();

  // 2a. Check an earlier question mid-draft; the draft comes back afterwards.
  await page.waitForTimeout(1_000);
  await page.getByRole("complementary", { name: "Recently added" }).getByRole("link", { name: new RegExp(`${tag}: 2 \\+ 2`) }).click();
  await page.waitForURL(/from=new/);
  await expect(page.getByLabel("Option B is correct")).toBeChecked();
  await page.getByRole("link", { name: "Back to the question you were writing" }).click();
  await page.waitForURL(/\/admin\/cbt\/questions\/new/);
  await expect(page.getByText(/Restored your unsaved changes/)).toBeVisible();
  await expect(page.getByLabel("Kind")).toHaveValue("Theory");
  await expect(text).toHaveValue(`${tag}: Explain why the sky looks blue.`);

  // 3. Fill in the gap: gap button, answer chips, student preview, then save back to the bank.
  await page.getByLabel("Kind").selectOption("FillInTheGap");
  await page.getByLabel("Sentence").fill(`${tag}: The capital of Nigeria is `);
  await page.getByRole("button", { name: "Insert gap" }).click();
  await expect(page.getByLabel("Sentence")).toHaveValue(`${tag}: The capital of Nigeria is ___`);
  await page.getByLabel("Accepted answers").fill("Abuja");
  await page.keyboard.press("Enter");
  await page.getByLabel("Accepted answers").fill("abuja ");
  await page.getByRole("button", { name: "Add answer" }).click();
  await expect(page.getByText("That answer is already in the list.")).toBeVisible();
  await page.getByLabel("Accepted answers").fill("FCT");
  await page.keyboard.press("Enter");
  await expect(page.getByRole("list", { name: "Answers so far" }).getByRole("listitem")).toHaveCount(2);
  await page.getByRole("button", { name: "Preview as student" }).click();
  const preview = page.getByRole("dialog", { name: "Preview as student" });
  await expect(preview.getByLabel("Your answer for the gap")).toBeVisible();
  await expect(preview).not.toContainText("Abuja");
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Save question" }).click();
  await page.waitForURL(/\/admin\/cbt\/bank\?/, { timeout: 30_000 });

  // 4. The bank lists both; search narrows to this run.
  await page.getByLabel("Search questions").fill(tag);
  await expect(page).toHaveURL(/search=E2E/, { timeout: 30_000 });
  await expect(page.getByRole("link", { name: new RegExp(`${tag}: 2 \\+ 2`) })).toBeVisible({ timeout: 30_000 });
  const gapRow = page.getByRole("link", { name: new RegExp(`${tag}: The capital`) });
  await expect(gapRow).toContainText("Fill in the gap");
  await expect(gapRow).toContainText("Auto-marked");

  // 5. Edit the objective: picture, difficulty, preview without keys, save; then archive it.
  await page.getByRole("link", { name: new RegExp(`${tag}: 2 \\+ 2`) }).click();
  await page.waitForURL(/\/admin\/cbt\/questions\/[0-9a-f-]+$/);
  await expect(page.getByLabel("Option B is correct")).toBeChecked();
  await page.locator('input[type="file"]').setInputFiles({ name: "diagram.png", mimeType: "image/png", buffer: PNG });
  await expect(page.getByText("Picture saved").first()).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("img", { name: "Illustration for the question" })).toBeVisible();
  await page.getByLabel("Difficulty").selectOption("Hard");
  await page.getByRole("button", { name: "Preview as student" }).click();
  const objectivePreview = page.getByRole("dialog", { name: "Preview as student" });
  await expect(objectivePreview.getByRole("radio")).toHaveCount(3);
  await expect(objectivePreview.getByRole("radio", { checked: true })).toHaveCount(0);
  await expect(objectivePreview.getByRole("img", { name: "Illustration for the question" })).toBeVisible();
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByText("Question saved").first()).toBeVisible({ timeout: 30_000 });
  await page.reload();
  await expect(page.getByLabel("Difficulty")).toHaveValue("Hard");
  await expect(page.getByRole("img", { name: "Illustration for the question" })).toBeVisible();

  await page.getByRole("button", { name: "Archive" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Archive" }).click();
  await page.waitForURL(/\/admin\/cbt\/bank\?/, { timeout: 30_000 });
  await page.getByLabel("Search questions").fill(tag);
  await expect(page).toHaveURL(/search=E2E/, { timeout: 30_000 });
  await expect(page.getByRole("link", { name: new RegExp(`${tag}: The capital`) })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("link", { name: new RegExp(`${tag}: 2 \\+ 2`) })).toHaveCount(0);
});

test("a bursar can't open the question bank", async ({ page }) => {
  await signIn(page, "bursar@maydel.test");
  await expect(page.getByRole("link", { name: "Question bank" })).toHaveCount(0);
  await page.goto("/admin/cbt");
  await expect(page.getByText(/don.t have access|not allowed|permission/i).first()).toBeVisible();
});
