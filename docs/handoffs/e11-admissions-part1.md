# Handoff → Frontend: E11 part 1 — Admission form purchase and applicant accounts (E11.4)

> Backend status as of 2026-10-17. Contract: `contracts/openapi.json` (run `npm run api:types`). Parts 2–4 (online form + PDF, exam booking and results, offers and letters) follow.

## Decisions (with the user)
- **One login throughout.** Buying the form creates the family's **applicant account** (email + password). That same account fills the form, books the exam and sees results, and becomes the **parent account** if a child is enrolled. Existing parents apply for siblings from their current account.
- **Pay online (Paystack) or at the school.** At the school, the bursar records the payment and issues a one-time **form code** (`ABCD-EFGH`; no 0/O or 1/I/L) that the family enters.
- **Unsuccessful applicants:** the account stays an Applicant with no access to school data. It can apply again later. Documents and child details are deleted about 6 months after the decision (NDPR), and accounts inactive for 12 months are deactivated. The clean-up job comes with part 4.

## New role
`Applicant` is added to the `Role` enum. Signing in works as usual (email + password). Route an applicant-only user to the **admissions area** (e.g. `/admissions`), not the staff console or the parent portal. A user who is both Parent and Applicant sees both.

## Endpoints
| Method & path | Who | Body / response | Errors |
|---|---|---|---|
| `GET /api/public/admissions` | anyone | `AdmissionsInfo[]`: open sessions with `formFee`, `examWindowDays`, `instructions`, `classLevels[]` | |
| `POST /api/public/admissions/register` | anyone (rate-limited) | `{ parentName, email, phone, password, childFirstName, childLastName, childDateOfBirth?, classLevelId, sessionId, website? }` → `ApplicationDto` | 400 (fields; `password` rules: ≥ 10 chars with upper, lower and a digit); 409 `admission.email_in_use` ("sign in and apply from your account"); 422 `admission.closed`; 429 |
| `GET /api/admissions/my-applications` | Applicant, Parent | `ApplicationDto[]`, newest first | |
| `POST /api/admissions/applications` | Applicant, Parent | `{ childFirstName, childLastName, childDateOfBirth?, classLevelId, sessionId }` → `ApplicationDto` (another child) | 400, 422 `admission.closed` |
| `POST /api/admissions/applications/{id}/redeem-code` | Applicant, Parent | `{ code }` (any case and spacing) → `ApplicationDto` | 400 `errors.code`; 404; 422 `admission.already_paid` |
| `POST /api/admissions/applications/{id}/pay` | Applicant, Parent | → `OnlineCheckout { reference ("MSF-…"), authorizationUrl, amount }` | 404; 422 `payment.online_unavailable` / `admission.already_paid` / `admission.free_form` |
| `GET /api/admissions/payments/{reference}` | Applicant, Parent | `{ reference, status: Pending\|Succeeded\|Failed\|NeedsReview, applicationId, message }` | 404 |
| `GET /api/admissions/settings?sessionId` | Admin, Bursar | `AdmissionSettingsDto { sessionId, session, isOpen, formFee, examWindowDays, instructions }` | 404 |
| `PUT /api/admissions/settings` | Admin | same fields → `AdmissionSettingsDto` | 400 `formFee` / `examWindowDays` (1–30) |
| `POST /api/admissions/form-codes` | Admin, Bursar | `{ sessionId, count (1–50), amountPaid, issuedTo? }` → `FormCodeDto[]` | 400 |
| `GET /api/admissions/form-codes?sessionId&used?` | Admin, Bursar | `PagedResult<FormCodeDto>` | |
| `GET /api/admissions/applications?sessionId&status?&classLevelId?&search?` | Admin, Bursar | `PagedResult<ApplicationDto>` | |

- `ApplicationDto`: `{ id, referenceNumber ("APP-2026-0001"), sessionId, session, classLevelId, classLevel, parentName, phone, email, childFirstName, childLastName, childDateOfBirth, status, formFee, formPaymentMethod: "Online"|"SchoolCode"|null, formPaidAt, examWindowEndsOn, createdAt }`.
- `status`: `AwaitingPayment | FormPaid | Submitted | ExamBooked | Missed | Assessed | Offered | NotOffered | Accepted | Enrolled | Withdrawn`. Only the first two are used so far.
- **Form paid →** the exam window opens (`examWindowEndsOn` = today + the session's days), and the family is emailed "your form is ready, book the exam by …".
- After a Paystack form payment, the family returns to `Paystack:AdmissionsCallbackUrl` (default `http://localhost:3000/admissions/paid?reference=…`). Poll `GET /api/admissions/payments/{reference}`, like the fees return page.

Also in this PR, for E10.8: `GET /api/finance/proofs?invoiceId=…` returns that invoice's proofs in every status.

## UI to build
1. **Public "Buy admission form" page** (`/admissions/apply`, from the website):
   - Session info (fee, instructions), then the form: parent name, email, phone, password, child's names, date of birth and class.
   - Include a hidden `website` honeypot field, labelled for bots only and visually hidden.
   - On success, sign the family in (call the normal login with the email and password) and go to `/admissions`.
   - On 409, show "You already have an account. Sign in to apply."
2. **Family admissions area (`/admissions`, Applicant/Parent):**
   - Application cards: reference, child, class, status badge, and the exam deadline once paid.
   - "Apply for another child".
   - For `AwaitingPayment`: a **Pay online** button (handle 422 `payment.online_unavailable` with "Pay at the school and enter your form code") and an **"I have a form code"** input.
   - `/admissions/paid`: the return page.
3. **Staff:**
   - **Settings → Admissions (admin):** a session picker, an Open/Closed switch, form fee, exam window days and instructions.
   - **Admissions → Form codes (bursar):** issue N codes (amount, issued to). Show them **once, large and copyable**, with a print-friendly slip listing the code, session and fee. List them with a used/unused filter.
   - **Admissions → Applications:** a table with search, status and class filters.
4. **Nav:** "Admissions" for Admin/Bursar. Applicant-only users see only the admissions area.
