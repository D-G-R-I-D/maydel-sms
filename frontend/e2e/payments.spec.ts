import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

// E10 part 2a on a fixture of its own: one fresh student in the "E2E Register" class (Creche level,
// no real students), a temporary Creche-only fee, invoices generated for Creche only. The bursar
// records the full payment; the admin reverses it, adds a discount and cancels the invoice. Afterwards
// the fee is deleted and the student withdrawn. Fixture setup talks to the API directly (test only).
const PASSWORD = "Demo#Pass2026";
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
test.setTimeout(360_000);

// What this run created, so afterEach can clean up even after a timeout.
const created: { studentId?: string; feeId?: string; invoiceId?: string } = {};

test.afterEach(async ({ request }) => {
  const api = await adminApi(request);
  if (created.invoiceId) {
    const inv = await api.get<{ status: string; payments: { id: string; isReversal: boolean; reversedBy: string | null }[] }>(`/api/finance/invoices/${created.invoiceId}`);
    for (const p of inv.payments ?? []) if (!p.isReversal && !p.reversedBy) await api.post(`/api/finance/payments/${p.id}/reverse`, { reason: "E2E clean-up" });
    if (inv.status !== "Cancelled") await api.post(`/api/finance/invoices/${created.invoiceId}/cancel`, { reason: "E2E clean-up" });
  }
  if (created.feeId) await api.del(`/api/finance/fee-items/${created.feeId}`);
  if (created.studentId) await api.post(`/api/students/${created.studentId}/status`, { status: "Withdrawn" });
  created.studentId = created.feeId = created.invoiceId = undefined;
});

async function signIn(page: Page, userName: string) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/(admin|portal)/);
}

async function adminApi(request: APIRequestContext) {
  const res = await request.post(`${API}/api/auth/login`, { data: { userName: "admin@maydel.test", password: PASSWORD } });
  expect(res.ok()).toBeTruthy();
  const headers = { Authorization: `Bearer ${(await res.json()).accessToken}` };
  return {
    get: async <T>(path: string): Promise<T> => (await request.get(`${API}${path}`, { headers })).json(),
    post: (path: string, data: unknown) => request.post(`${API}${path}`, { headers, data }),
    del: (path: string) => request.delete(`${API}${path}`, { headers }),
  };
}

test("record a payment, reverse it, adjust and cancel the invoice", async ({ page, request }) => {
  // Playwright can't drive the native Save As dialog; without it PDF buttons fall back to a download.
  await page.addInitScript(() => {
    delete (window as { showSaveFilePicker?: unknown }).showSaveFilePicker;
  });
  const api = await adminApi(request);
  const levels = await api.get<{ id: string; name: string }[]>("/api/academics/class-levels");
  const creche = levels.find((l) => l.name === "Creche")!;
  const rooms = await api.get<{ id: string; arm: string; classLevelId: string; studentCount: number }[]>("/api/academics/classrooms");
  test.skip(rooms.some((r) => r.classLevelId === creche.id && r.arm !== "E2E Register" && r.studentCount > 0), "real Creche students exist; generating would bill them");
  let room = rooms.find((r) => r.arm === "E2E Register");
  if (!room) {
    const created = await api.post("/api/academics/classrooms", { classLevelId: creche.id, arm: "E2E Register", capacity: 40, formTeacherId: null });
    room = await created.json();
  }
  const sessions = await api.get<{ terms: { id: string; isCurrent: boolean }[] }[]>("/api/academics/sessions");
  const termId = sessions.flatMap((s) => s.terms).find((t) => t.isCurrent)!.id;
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Lagos" }).format(new Date());
  const suffix = `${Date.now() % 100000}`;

  const admitted = await api.post("/api/students", {
    firstName: "Fees",
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
  expect(admitted.ok()).toBeTruthy();
  const student = await admitted.json();
  created.studentId = student.id;
  const fee = await (await api.post("/api/finance/fee-items", { termId, name: `E2E fee tuition ${suffix}`, amount: 50000, classLevelId: creche.id, isMandatory: true, siblingDiscount: false })).json();
  created.feeId = fee.id;

  try {
    const gen = await api.post("/api/finance/invoices/generate", { termId, classLevelId: creche.id });
    expect(gen.ok()).toBeTruthy();
    const list = await api.get<{ items: { id: string; number: string }[] }>(`/api/finance/invoices?termId=${termId}&search=${student.admissionNumber}`);
    const invoice = list.items[0];
    expect(invoice).toBeTruthy();
    created.invoiceId = invoice.id;
    // Other Creche fees may exist for the term, so read the total rather than assume it.
    const { total } = await api.get<{ total: number }>(`/api/finance/invoices/${invoice.id}`);
    const naira = (n: number) => `₦${n.toLocaleString("en-NG", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

    // Generated invoices are drafts until published (E10.7). The bursar publishes Creche only, no emails.
    await signIn(page, "bursar@maydel.test");
    await page.goto(`/admin/invoices/${invoice.id}`);
    await expect(page.getByRole("heading", { name: invoice.number })).toBeVisible();
    await expect(page.getByText("Draft: not visible to parents yet")).toBeVisible();
    await page.goto("/admin/invoices");
    await expect(async () => {
      await page.getByRole("button", { name: /^Publish invoices/ }).click();
      await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    const pub = page.getByRole("dialog");
    await pub.getByLabel("Which").selectOption({ label: "Creche only" });
    await pub.getByLabel("Email parents a copy").uncheck();
    await pub.getByRole("button", { name: "Publish", exact: true }).click();
    await expect(page.getByText(/^Published \d+/).first()).toBeVisible({ timeout: 30_000 });
    await page.goto(`/admin/invoices/${invoice.id}`);
    await expect(page.getByRole("heading", { name: invoice.number })).toBeVisible();
    await expect(page.getByText("Draft: not visible to parents yet")).toHaveCount(0);

    // Bursar records the full payment (no instalment plan → full balance only).
    await expect(async () => {
      await page.getByRole("button", { name: "Record payment" }).click();
      await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    const dialog = page.getByRole("dialog");
    await dialog.getByLabel("Method").selectOption("BankTransfer");
    await dialog.getByLabel("Reference (optional)").fill("E2E-REF");
    await dialog.getByRole("button", { name: "Record payment" }).click();
    await expect(dialog.getByText(/^Receipt RCT-/)).toBeVisible({ timeout: 30_000 });
    await expect(dialog).toContainText(student.admissionNumber);
    await expect(dialog).toContainText(naira(total));
    const receiptNumber = (await dialog.getByText(/^Receipt RCT-/).innerText()).replace("Receipt ", "");
    await dialog.getByRole("button", { name: "Done" }).click();

    // Invoice PDF (staff) downloads; the receipt PDF is served for the payment.
    const download = page.waitForEvent("download", { timeout: 60_000 });
    await page.getByRole("button", { name: "Invoice PDF" }).click();
    expect((await download).suggestedFilename()).toMatch(/^INV-.*\.pdf$/);
    const paymentId = (await api.get<{ payments: { id: string; receiptNumber: string }[] }>(`/api/finance/invoices/${invoice.id}`)).payments.find((p) => p.receiptNumber === receiptNumber)!.id;
    const receiptPdf = await page.request.get(`/api/media/finance/receipts/${paymentId}/pdf`);
    expect(receiptPdf.status()).toBe(200);
    expect((await receiptPdf.body()).subarray(0, 4).toString()).toBe("%PDF");
    await expect(page.getByText("Paid", { exact: true }).first()).toBeVisible({ timeout: 30_000 });
    await expect(page.getByRole("button", { name: "Reverse" })).toHaveCount(0); // bursars can't reverse

    // Admin reverses it, adds a discount, then cancels the (now empty) invoice.
    await signIn(page, "admin@maydel.test");
    await page.goto(`/admin/invoices/${invoice.id}`);
    await expect(async () => {
      await page.getByRole("button", { name: "Reverse" }).click();
      await expect(page.getByRole("alertdialog")).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    await page.getByLabel("Reason").fill("E2E: wrong student");
    await page.getByRole("alertdialog").getByRole("button", { name: "Reverse payment" }).click();
    await expect(page.getByText(/^Reverses RCT-/)).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(/^Reversed by RCT-/)).toBeVisible();

    await page.getByRole("button", { name: "Add adjustment" }).click();
    const adj = page.getByRole("dialog");
    await adj.getByLabel("Description").fill("E2E scholarship");
    await adj.getByLabel("Amount (₦)").fill("5,000");
    await expect(adj).toContainText(`New total ${naira(total - 5000)}`);
    await adj.getByRole("button", { name: "Add adjustment" }).click();
    await expect(page.getByRole("cell", { name: "E2E scholarship" })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText("−₦5,000.00").first()).toBeVisible();

    await page.getByRole("button", { name: "Cancel invoice" }).click();
    await page.getByLabel("Reason").fill("E2E clean-up");
    await page.getByRole("alertdialog").getByRole("button", { name: "Cancel invoice" }).click();
    await expect(page.getByText(/Generate invoices again to bill this student/)).toBeVisible({ timeout: 30_000 });

    // Statement and debtors.
    await page.goto(`/admin/students/${student.id}?tab=fees`);
    await expect(page.getByRole("navigation", { name: "Student sections" }).getByRole("link", { name: "Fees" })).toHaveAttribute("aria-current", "page");
    await expect(page.getByText("Billed", { exact: true })).toBeVisible(); // cancelled invoices drop off the statement
    await page.goto("/admin/debtors");
    await expect(page.getByText("Total outstanding")).toBeVisible();
    const csv = await page.request.get(`/api/media/finance/debtors-csv?termId=${termId}`);
    expect(csv.status()).toBe(200);
    expect(csv.headers()["content-type"]).toContain("text/csv");
  } finally {
    // afterEach does the clean-up (it also runs after a timeout).
  }
});

test("online payments list with status filters (bursar)", async ({ page }) => {
  await signIn(page, "bursar@maydel.test");
  await page.goto("/admin/payments");
  await expect(page.getByRole("heading", { name: "Online payments", level: 1 })).toBeVisible();
  await page.getByRole("navigation", { name: "Filter by status" }).getByRole("link", { name: /Needs review/ }).click();
  await expect(page).toHaveURL(/status=NeedsReview/);
  await expect(page.getByRole("table").or(page.getByText("Nothing to review"))).toBeVisible();
});

test("proof of payment: student sends it, bursar rejects, student sends again, bursar confirms", async ({ page, request }) => {
  const api = await adminApi(request);
  const levels = await api.get<{ id: string; name: string }[]>("/api/academics/class-levels");
  const creche = levels.find((l) => l.name === "Creche")!;
  const rooms = await api.get<{ id: string; arm: string; classLevelId: string; studentCount: number }[]>("/api/academics/classrooms");
  test.skip(rooms.some((r) => r.classLevelId === creche.id && r.arm !== "E2E Register" && r.studentCount > 0), "real Creche students exist; generating would bill them");
  let room = rooms.find((r) => r.arm === "E2E Register");
  if (!room) room = await (await api.post("/api/academics/classrooms", { classLevelId: creche.id, arm: "E2E Register", capacity: 40, formTeacherId: null })).json();
  const sessions = await api.get<{ terms: { id: string; isCurrent: boolean }[] }[]>("/api/academics/sessions");
  const termId = sessions.flatMap((x) => x.terms).find((t) => t.isCurrent)!.id;
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Lagos" }).format(new Date());
  const suffix = `${Date.now() % 100000}`;
  const student = await (
    await api.post("/api/students", {
      firstName: "Proof",
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
    })
  ).json();
  created.studentId = student.id;
  const fee = await (await api.post("/api/finance/fee-items", { termId, name: `E2E fee proof ${suffix}`, amount: 30000, classLevelId: creche.id, isMandatory: true, siblingDiscount: false })).json();
  created.feeId = fee.id;
  expect((await api.post("/api/finance/invoices/generate", { termId, classLevelId: creche.id })).ok()).toBeTruthy();
  expect((await api.post("/api/finance/invoices/publish", { termId, classLevelId: creche.id, notifyParents: false })).ok()).toBeTruthy();
  const invoice = (await api.get<{ items: { id: string; number: string }[] }>(`/api/finance/invoices?termId=${termId}&search=${student.admissionNumber}`)).items[0];
  created.invoiceId = invoice.id;
  const creds = await (await api.post(`/api/students/${student.id}/login`, {})).json();

  // The student signs in (first time: must change the temporary password).
  const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC", "base64");
  const signInStudent = async (password: string) => {
    await page.context().clearCookies();
    await page.goto("/login");
    await page.getByLabel("Email or admission number").fill(creds.userName);
    await page.getByLabel("Password", { exact: true }).fill(password);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await page.waitForURL(/\/(portal|change-password)/);
  };
  const newPassword = `E2e#Proof${suffix}x`;
  await signInStudent(creds.temporaryPassword);
  if (page.url().includes("/change-password")) {
    await page.getByLabel("Current password").fill(creds.temporaryPassword);
    await page.getByLabel("New password", { exact: true }).fill(newPassword);
    await page.getByLabel("Confirm new password").fill(newPassword);
    await page.getByRole("button", { name: /Change password/ }).click();
    await page.waitForURL(/\/portal/);
  }

  const sendProof = async (buttonName: RegExp) => {
    await page.goto("/portal/fees");
    await expect(async () => {
      await page.getByRole("button", { name: buttonName }).first().click();
      await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    const dialog = page.getByRole("dialog");
    await dialog.locator('input[type="file"][accept*="pdf"]').setInputFiles({ name: "transfer.png", mimeType: "image/png", buffer: PNG });
    await dialog.getByLabel("Amount paid (₦)").fill("30,000");
    await dialog.getByLabel("Bank reference (optional)").fill(`E2E-${suffix}`);
    await dialog.getByRole("button", { name: "Send proof" }).click();
    await expect(page.getByText("Proof sent").first()).toBeVisible({ timeout: 30_000 });
  };

  await sendProof(/I've paid by bank transfer/);
  await expect(page.getByText("Being checked").first()).toBeVisible({ timeout: 30_000 });

  // Bursar rejects it with a reason.
  await signIn(page, "bursar@maydel.test");
  await page.goto("/admin/proofs");
  let row = page.getByRole("row").filter({ hasText: `E2E${suffix}` });
  await expect(row).toHaveCount(1, { timeout: 30_000 });
  await expect(async () => {
    await row.getByRole("button", { name: "Reject" }).click();
    await expect(page.getByRole("alertdialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByLabel("Reason").fill("E2E: no matching credit");
  await page.getByRole("alertdialog").getByRole("button", { name: "Reject proof" }).click();
  await expect(page.getByText(/Proof rejected/).first()).toBeVisible({ timeout: 30_000 });

  // The student sees why and sends it again.
  await signInStudent(newPassword);
  await page.goto("/portal/fees");
  await expect(page.getByText("Reason: E2E: no matching credit")).toBeVisible({ timeout: 30_000 });
  await sendProof(/^Send again$/);

  // Bursar confirms: payment recorded with a receipt.
  await signIn(page, "bursar@maydel.test");
  await page.goto("/admin/proofs");
  row = page.getByRole("row").filter({ hasText: `E2E${suffix}` });
  await expect(row).toHaveCount(1, { timeout: 30_000 });
  await expect(async () => {
    await row.getByRole("button", { name: "Confirm" }).click();
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  const confirm = page.getByRole("dialog");
  await expect(confirm.getByLabel("Amount received (₦)")).toHaveValue("30000.00");
  await confirm.getByRole("button", { name: "Confirm and record" }).click();
  await expect(confirm.getByText(/^Receipt RCT-/)).toBeVisible({ timeout: 30_000 });
});
