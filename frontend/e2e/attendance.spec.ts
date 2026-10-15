import { expect, test, type Page } from "@playwright/test";

// E9.1–E9.2 daily register. Read-only against shared data: Year 3 A's reports are published, so its
// register rows are locked, and nothing here saves a register.
const PASSWORD = "Demo#Pass2026";
test.setTimeout(150_000);

async function signIn(page: Page, userName: string) {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

test("teachers land on their form class's register for today", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/attendance");
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(/register$/);
  const groups = page.getByRole("radiogroup", { name: /^Attendance for / });
  const noRegister = page.getByText("No register this day");
  await expect(groups.first().or(noRegister)).toBeVisible({ timeout: 30_000 });
  test.skip(await noRegister.isVisible(), "no school today (weekend, mid-term or outside a term)");

  // Each student has P/A/L/E; locked rows (published reports) say so and can't change.
  await expect(groups.first().getByRole("radio")).toHaveCount(4);
  const locked = page.getByText("Report published");
  if ((await locked.count()) > 0) await expect(groups.first().getByRole("radio", { name: "Absent" })).toBeDisabled();
  await expect(page.getByText(/\d+ present/)).toBeVisible();
});

test("earlier days are read-only for teachers; the date controls move by school day", async ({ page }) => {
  await signIn(page, "teacher@maydel.test");
  await page.goto("/admin/attendance");
  await expect(page.getByRole("button", { name: "Next school day" })).toBeDisabled();
  await page.getByRole("button", { name: "Previous school day" }).click();
  await expect(page).toHaveURL(/date=\d{4}-\d{2}-\d{2}/);
  const viewOnly = page.getByText("Only an administrator can change earlier days.");
  const noRegister = page.getByText("No register this day");
  await expect(viewOnly.or(noRegister)).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("button", { name: "Save register" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Today" })).toBeVisible();
});

test("term summary lists attendance per student", async ({ page }) => {
  await signIn(page, "admin@maydel.test");
  await page.goto("/admin/attendance?tab=summary");
  await expect(page.getByRole("link", { name: "Term summary" })).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("columnheader", { name: "%" }).or(page.getByText("No registers taken this term"))).toBeVisible({ timeout: 30_000 });
});

test("bursars can't open attendance", async ({ page }) => {
  await signIn(page, "bursar@maydel.test");
  await page.goto("/admin/attendance");
  await expect(page.getByText("You don't have access to this page")).toBeVisible();
});

/* --------------------------------------------------------------------------------------------- */
// Save path on a fixture of its own (never Year 3 A): an "E2E Register" class, reused across runs,
// with two fresh students per run, withdrawn at the end. Fixture setup talks to the API directly
// (test infrastructure only; the app itself never does).
const API = process.env.E2E_API_URL ?? "http://localhost:5178";

async function adminApi(request: import("@playwright/test").APIRequestContext) {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName: "admin@maydel.test", password: PASSWORD } });
  expect(res.ok()).toBeTruthy();
  const body = await res.json();
  const token: string = body.accessToken ?? body.tokens?.accessToken;
  const headers = { Authorization: `Bearer ${token}` };
  return {
    get: async (path: string) => (await request.get(`${API}${path}`, { headers })).json(),
    post: (path: string, data: unknown) => request.post(`${API}${path}`, { headers, data }),
  };
}

test("admin takes a register, saves changes and finds them after a reload", async ({ page, request }) => {
  const api = await adminApi(request);
  const classrooms: { id: string; arm: string; classLevelId: string }[] = await api.get("/api/academics/classrooms");
  let room = classrooms.find((c) => c.arm === "E2E Register");
  if (!room) {
    const levels: { id: string }[] = await api.get("/api/academics/class-levels");
    const created = await api.post("/api/academics/classrooms", { classLevelId: levels[0].id, arm: "E2E Register", capacity: 40, formTeacherId: null });
    expect(created.ok()).toBeTruthy();
    room = await created.json();
  }
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Lagos" }).format(new Date());
  const suffix = `${Date.now() % 100000}`;
  const student = (firstName: string) => ({
    firstName,
    middleName: null,
    lastName: `E2E${suffix}`,
    dateOfBirth: null,
    sex: null,
    stateOfOrigin: null,
    lga: null,
    homeAddress: null,
    house: null,
    medicalNotes: null,
    classRoomId: room!.id,
    admittedOn: today,
  });
  const ids: string[] = [];
  for (const name of ["Reg One", "Reg Two"]) {
    const res = await api.post("/api/students", student(name));
    expect(res.ok()).toBeTruthy();
    ids.push((await res.json()).id);
  }

  try {
    await signIn(page, "admin@maydel.test");
    await page.goto(`/admin/attendance?classRoomId=${room!.id}`);
    const noRegister = page.getByText("No register this day");
    const one = page.getByRole("radiogroup", { name: new RegExp(`^Attendance for (?=.*Reg One)(?=.*E2E${suffix})`) });
    await expect(one.first().or(noRegister)).toBeVisible({ timeout: 30_000 });
    test.skip(await noRegister.isVisible(), "no school today (weekend, mid-term or outside a term)");

    const save = page.getByRole("button", { name: "Save register" });
    await expect(async () => {
      await one.first().getByRole("radio", { name: "Absent" }).check({ force: true });
      await expect(save).toBeEnabled({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    await page.getByRole("textbox", { name: /^Note for .*Reg One/ }).fill("E2E: unwell");
    await save.click();
    await expect(page.getByText("Register saved")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(/^Saved \d{2}:\d{2}$/)).toBeVisible();

    await page.reload();
    await expect(one.first().getByRole("radio", { name: "Absent" })).toBeChecked({ timeout: 30_000 });
    await expect(page.getByRole("textbox", { name: /^Note for .*Reg One/ })).toHaveValue("E2E: unwell");
    await expect(page.getByText("Register taken")).toBeVisible();

    // Change back and save again.
    await one.first().getByRole("radio", { name: "Present" }).check({ force: true });
    await save.click();
    await expect(page.getByText("Register saved").last()).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(/^\d+ absent$/)).toHaveText("0 absent");
  } finally {
    for (const id of ids) await api.post(`/api/students/${id}/status`, { status: "Withdrawn" });
  }
});
