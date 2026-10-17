# Handoff → Frontend: E11 part 2 — Online application form (E11.5)

> Backend status as of 2026-10-17. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on part 1 (`e11-admissions-part1.md`).

## How it works
1. Once the form is paid for (`status: FormPaid`), the family fills the form. It can be **saved as a draft** any number of times, with blank fields allowed.
2. They attach documents: a **passport photograph** (required, photo only), a birth certificate, the last report card, or other files.
   - Photos (JPEG/PNG/WebP ≤ 12 MB) are re-encoded. PDFs must be ≤ 5 MB.
   - At most **6 documents**. Uploading a new passport photo replaces the old one.
3. **Submit** checks the essentials:
   - the child's date of birth and sex, and the home address;
   - **at least one parent's** name and phone (father or mother);
   - the declaration ticked;
   - the passport photo uploaded.
   
   The status then becomes `Submitted`, the form and documents are **locked** (422 `admission.form_locked`), and the family gets an "application received, take the exam by …" email.
4. **PDFs:**
   - A **blank form** (public, to print and fill by hand) for a session.
   - The **filled form** (family or staff) with the passport photo, which works as a draft preview before submission.
5. Documents are private (the family's own application, or finance/admin staff) and always served as a **download**.

## Endpoints
| Method & path | Who | Body / response | Errors |
|---|---|---|---|
| `GET /api/public/admissions/blank-form.pdf?sessionId` | anyone | PDF | 404 |
| `GET /api/admissions/applications/{id}` | Applicant, Parent (own) | `ApplicationDetail` | 404 |
| `PUT /api/admissions/applications/{id}/form` | Applicant, Parent (own) | `SaveFormRequest` → `ApplicationDetail` | 400 (field lengths, emails, `childFirstName`); 404; 422 `admission.form_locked` |
| `POST /api/admissions/applications/{id}/documents` | Applicant, Parent (own) | **multipart**: `kind` (`PassportPhoto\|BirthCertificate\|LastReportCard\|Other`), `file` → `ApplicationDetail` | 400 `file` / `kind`; 404; 422 `admission.form_locked` / `admission.too_many_documents` |
| `DELETE /api/admissions/applications/{id}/documents/{documentId}` | Applicant, Parent (own) | → `ApplicationDetail` | 404; 422 `admission.form_locked` |
| `GET /api/admissions/applications/{id}/documents/{documentId}/file` | Applicant, Parent (own) | file download | 404 |
| `POST /api/admissions/applications/{id}/submit` | Applicant, Parent (own) | → `ApplicationDetail` | 400 (field = first missing item: `childDateOfBirth`, `childSex`, `homeAddress`, `fatherName`, `declaration`, `documents`); 404; 422 `admission.form_locked` |
| `GET /api/admissions/applications/{id}/form.pdf` | Applicant, Parent (own) | PDF | 404 |
| `GET /api/admissions/applications/{id}/detail` | Admin, Bursar | `ApplicationDetail` | 404 |
| `GET /api/admissions/applications/{id}/documents/{documentId}/download` | Admin, Bursar | file download | 404 |
| `GET /api/admissions/applications/{id}/form-pdf` | Admin, Bursar | PDF | 404 |
| `PUT /api/admissions/applications/{id}/correction` | **Admin** | `SaveFormRequest` → `ApplicationDetail` | 400 (first missing field: the form must stay complete); 404; 422 `admission.not_submitted` (family can still edit it) / `admission.closed_application` (enrolled or withdrawn) |

```ts
type ApplicationForm = { childMiddleName; childSex: "Female" | "Male" | null; stateOfOrigin; lga; religion; homeAddress; previousSchool;
  previousClass; medicalNotes; specialNeeds; fatherName; fatherPhone; fatherEmail; fatherOccupation; motherName; motherPhone; motherEmail;
  motherOccupation; emergencyContactName; emergencyContactPhone; declaration: boolean }; // strings nullable
type SaveFormRequest = { childFirstName; childLastName; childDateOfBirth: string | null; form: ApplicationForm };
type ApplicationDetail = { application: ApplicationDto; form: ApplicationForm;
  documents: { id; kind; contentType; uploadedAt }[]; submittedAt: string | null; canEdit: boolean; missingToSubmit: string[] };
```
- Lengths: names, occupations, state, LGA, religion, schools ≤ 120; phones ≤ 30; address ≤ 300; medical notes and special needs ≤ 1000.
- `missingToSubmit` lists what's still needed. Use it to show a checklist and to enable the Submit button.

## UI to build
1. **Family `/admissions/{id}`** (from the application card when `FormPaid`):
   - Sections: Child · Father/Guardian · Mother/Guardian · Emergency contact · Medical · Documents · Declaration.
   - **Save draft** (also auto-save on section change is fine), and a "still needed" checklist from `missingToSubmit`.
   - Documents: a passport photo slot (photo with preview, camera allowed on mobile), plus a list for the others with kind select and remove.
   - **Submit** opens a confirm dialog ("You can't change the form after submitting"). Afterwards, show read-only with "Submitted on …" and **Download form (PDF)**.
   - **Preview PDF** is available any time.
2. **Public apply page:** a "Download blank form (PDF)" link per open session.
3. **Staff Admissions → Applications → detail:** read-only form, documents (download), and **Form PDF**.
   - **Admin only:** an **Edit** button once the application is submitted (hidden for `AwaitingPayment`, `FormPaid`, `Enrolled` and `Withdrawn`). It opens the same form, prefilled, and saves to `PUT …/correction`. Changes are audited automatically. Documents can't be changed here.
4. Exam booking and scores come in part 3.
