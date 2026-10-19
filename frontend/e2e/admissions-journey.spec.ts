import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E11.5–E11.8: one family's whole journey on its own test applicant ("E2E…"), from form to enrolment.
// It changes shared settings only temporarily (admissions open, cut-off) and puts them back; the
// enrolled test student is withdrawn afterwards. The booked "E2E Hall" sitting stays (booked sittings
// can't be deleted). Publishing decisions is skipped if non-test applicants are waiting for one.
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
test.setTimeout(420_000);

const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC", "base64");

async function signIn(page: Page, userName: string, password = PASSWORD) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal|admissions)/);
}

async function api(request: APIRequestContext, userName = "admin@maydel.test", password = PASSWORD) {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName, password } });
  expect(res.ok()).toBeTruthy();
  const headers = { Authorization: `Bearer ${(await res.json()).accessToken}` };
  return {
    get: async <T>(path: string): Promise<T> => (await request.get(`${API}${path}`, { headers })).json(),
    post: (path: string, data: unknown = {}) => request.post(`${API}${path}`, { headers, data }),
    put: (path: string, data: unknown) => request.put(`${API}${path}`, { headers, data }),
  };
}

type Settings = { sessionId: string; isOpen: boolean; formFee: number; examWindowDays: number; instructions: string | null; entryTermId?: string | null; cutOffMark?: number | null };
const restore: { settings?: Settings; cutOff?: number | null; studentId?: string } = {};

test.afterAll(async ({ request }) => {
  const admin = await api(request);
  if (restore.settings) {
    const { sessionId, isOpen, formFee, examWindowDays, instructions, entryTermId } = restore.settings;
    await admin.put("/api/admissions/settings", { sessionId, isOpen, formFee, examWindowDays, instructions, entryTermId: entryTermId ?? null });
    if (restore.cutOff !== undefined) await admin.put("/api/admissions/results/cut-off", { sessionId, cutOffMark: restore.cutOff });
  }
  if (restore.studentId) await admin.post(`/api/students/${restore.studentId}/status`, { status: "Withdrawn" });
});

test("a family goes from form to enrolment", async ({ page, request }) => {
  const admin = await api(request);
  const sessions = await admin.get<{ id: string; isCurrent: boolean }[]>("/api/academics/sessions");
  const sessionId = (sessions.find((s) => s.isCurrent) ?? sessions[0]).id;
  const settings = await admin.get<Settings>(`/api/admissions/settings?sessionId=${sessionId}`);
  const before = await admin.get<{ cutOffMark: number | null; taken: { childName: string; decision: string | null }[] }>(`/api/admissions/results?sessionId=${sessionId}`);
  restore.settings = settings;
  restore.cutOff = before.cutOffMark;
  if (!settings.isOpen) expect((await admin.put("/api/admissions/settings", { ...settings, isOpen: true })).ok()).toBeTruthy();

  // Setup through the API (part 1's UI has its own test): register, then pay with a school form code.
  const suffix = `${Date.now() % 1_000_000}`;
  const email = `e2e+j${suffix}@maydel.test`;
  // Guardians are matched by phone (one guardian per number), so every run needs its own numbers or
  // enrolment links an earlier run's guardian and won't give this login the Parent role.
  const digits = () => String(Math.floor(Math.random() * 1e7)).padStart(7, "0");
  const parentPhone = `0803 ${digits()}`;
  const fatherPhone = `0805 ${digits()}`;
  const password = `E2e#Journey${suffix}`;
  const info = await admin.get<{ sessionId: string; isOpen: boolean; classLevels: { id: string; name: string }[] }[]>("/api/public/admissions");
  const level = info.find((s) => s.sessionId === sessionId)!.classLevels[0];
  const reg = await request.post(`${API}/api/public/admissions/register`, {
    data: { parentName: `E2E Journey ${suffix}`, email, phone: parentPhone, password, childFirstName: "Zuri", childLastName: `E2E${suffix}`, childDateOfBirth: null, classLevelId: level.id, sessionId, website: "" },
  });
  expect(reg.ok()).toBeTruthy();
  const appId: string = (await reg.json()).id;
  const codes = await (await admin.post("/api/admissions/form-codes", { sessionId, count: 1, amountPaid: settings.formFee || 0, issuedTo: `E2E Journey ${suffix}` })).json();
  const family = await api(request, email, password);
  expect((await family.post(`/api/admissions/applications/${appId}/redeem-code`, { code: codes[0].code })).ok()).toBeTruthy();

  // 1. Form: photo, required fields, draft survives a reload, then submit.
  await signIn(page, email, password);
  await page.goto(`/admissions/${appId}`);
  await expect(page.getByRole("heading", { name: `Zuri E2E${suffix}` })).toBeVisible();
  await expect(page.getByText("A passport photograph").first()).toBeVisible();
  await page.locator('input[type="file"][accept="image/jpeg,image/png,image/webp"]').setInputFiles({ name: "passport.png", mimeType: "image/png", buffer: PNG });
  await expect(page.getByText("Passport photograph uploaded")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("img", { name: "Passport photograph" })).toBeVisible({ timeout: 30_000 });

  const field = (label: string) => page.getByLabel(label, { exact: true });
  await expect(async () => {
    await field("Date of birth").fill("2021-03-04");
    await expect(field("Date of birth")).toHaveValue("2021-03-04", { timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await field("Sex").selectOption("Female");
  await field("Home address").fill("E2E: 12 Test Road, Abuja");
  await page.getByLabel("Full name", { exact: true }).first().fill(`E2E Father ${suffix}`);
  await page.getByLabel("Phone", { exact: true }).first().fill(fatherPhone);
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(page.getByText("Draft saved").first()).toBeVisible({ timeout: 30_000 });
  await page.reload();
  await expect(field("Home address")).toHaveValue("E2E: 12 Test Road, Abuja");
  await expect(page.getByRole("list", { name: "Still needed to submit" })).toContainText("Tick the declaration");

  await page.getByLabel(/I confirm that the information/).check();
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(page.getByRole("list", { name: "Still needed to submit" })).toContainText("Everything needed is here", { timeout: 30_000 });
  await page.getByRole("button", { name: "Submit application" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Submit" }).click();
  await expect(page.getByText(/^Submitted on /)).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("heading", { name: "Book your entrance exam" })).toBeVisible();

  // 2. Admin adds a sitting; the family books it.
  await signIn(page, "admin@maydel.test");
  await page.goto(`/admin/admissions/exams?sessionId=${sessionId}`);
  await expect(async () => {
    await page.getByRole("button", { name: "Add sitting" }).click();
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  const tomorrow = new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Lagos" }).format(new Date(Date.now() + 24 * 3600 * 1000));
  const slotDialog = page.getByRole("dialog");
  await slotDialog.getByLabel("Date").fill(tomorrow);
  await slotDialog.getByLabel("Starts at (Abuja time, WAT)").fill("09:00");
  await slotDialog.getByLabel("Venue").fill(`E2E Hall ${suffix}`);
  await slotDialog.getByLabel("Seats").fill("5");
  await slotDialog.getByRole("button", { name: "Add sitting" }).click();
  await expect(page.getByText("Sitting added").first()).toBeVisible({ timeout: 30_000 });

  await signIn(page, email, password);
  await page.goto(`/admissions/${appId}`);
  const slotRow = page.getByRole("listitem").filter({ hasText: `E2E Hall ${suffix}` });
  await slotRow.getByRole("button", { name: "Book" }).click();
  await expect(page.getByRole("heading", { name: "Entrance exam booked" })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/Bring a pencil, pen and eraser/)).toBeVisible();

  // 3. A teacher enters the score on the attendance sheet.
  await signIn(page, "teacher@maydel.test");
  await page.goto(`/admin/admissions/exams?sessionId=${sessionId}`);
  await page.getByRole("link").filter({ hasText: `E2E Hall ${suffix}` }).click();
  await page.waitForURL(/\/admin\/admissions\/exams\/[0-9a-f-]+/);
  // The printable sheet lists the booked child (window.print is stubbed in the test browser).
  await page.evaluate(() => (window.print = () => {}));
  await page.getByRole("button", { name: "Print attendance sheet" }).click();
  await expect(page.getByTestId("attendance-sheet")).toContainText(`Zuri E2E${suffix}`);
  const scoreBox = page.getByLabel(`Score for Zuri E2E${suffix}`, { exact: true });
  await expect(async () => {
    await scoreBox.fill("78.5");
    await expect(page.getByRole("button", { name: `Save score for Zuri E2E${suffix}` })).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByRole("button", { name: `Save score for Zuri E2E${suffix}` }).click();
  await expect(page.getByText(`Score saved for Zuri E2E${suffix}`)).toBeVisible({ timeout: 30_000 });

  // 4. Admin: cut-off, tick the offer, publish (only when no real applicants are waiting on a decision).
  const now = await admin.get<{ taken: { childName: string; status: string }[] }>(`/api/admissions/results?sessionId=${sessionId}`);
  const realWaiting = now.taken.filter((c) => c.status === "Assessed" && !c.childName.includes("E2E"));
  test.skip(realWaiting.length > 0, "real applicants are awaiting decisions; publishing would decide for them");

  await signIn(page, "admin@maydel.test");
  await page.goto(`/admin/admissions/results?sessionId=${sessionId}`);
  await expect(async () => {
    await page.getByLabel("Cut-off mark (%)").fill("50");
    await expect(page.getByRole("button", { name: "Apply" })).toBeEnabled({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByRole("button", { name: "Apply" }).click();
  await expect(page.getByText("Cut-off set to 50%").first()).toBeVisible({ timeout: 30_000 });
  const offer = page.getByRole("checkbox", { name: `Offer a place to Zuri E2E${suffix}` });
  await offer.click();
  await expect(offer).toBeChecked({ timeout: 30_000 });
  await expect(page.getByText("Ticked for an offer").first()).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: "Publish decisions" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Publish decisions" }).click();
  await expect(page.getByText(/^Offer letters sent to \d+ famil/).first()).toBeVisible({ timeout: 60_000 });

  // 5. The family accepts the offer.
  await signIn(page, email, password);
  await page.goto(`/admissions/${appId}`);
  await expect(page.getByText(/has been offered a place/)).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("button", { name: "Download letter" })).toBeVisible();
  await page.getByRole("button", { name: "Accept the offer" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Accept" }).click();
  await expect(page.getByRole("heading", { name: "Offer accepted" })).toBeVisible({ timeout: 30_000 });

  // 6. Admin enrols (no class), then the family signs in again and lands in the parent portal.
  await signIn(page, "admin@maydel.test");
  await page.goto(`/admin/admissions/${appId}`);
  await expect(async () => {
    await page.getByRole("button", { name: "Enrol" }).click();
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByRole("dialog").getByLabel("Class").selectOption("");
  await page.getByRole("dialog").getByRole("button", { name: "Enrol" }).click();
  // The page's own "enrolled" alert (the info card already showed the number from the offer).
  await expect(page.getByRole("status").filter({ hasText: /is enrolled as MSS\// })).toBeVisible({ timeout: 60_000 });
  const detail = await admin.get<{ application: { studentId: string | null } }>(`/api/admissions/applications/${appId}/detail`);
  restore.studentId = detail.application.studentId ?? undefined;

  await signIn(page, email, password);
  await expect(page).toHaveURL(/\/portal/);
  await page.goto("/admissions");
  await expect(page.getByRole("listitem").filter({ hasText: `Zuri E2E${suffix}` })).toContainText("Enrolled");
});
