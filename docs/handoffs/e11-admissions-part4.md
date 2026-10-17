# Handoff → Frontend: E11 part 4 — Offers, admission letters and enrolment (E11.8, E11.2)

> Backend status as of 2026-10-17. Contract: `contracts/openapi.json` (run `npm run api:types`). Completes the admissions flow (parts 1–4).

## How it works
1. **Decide (admin, draft):** from the results lists, tick applicants for an offer (`Offer`), or clear the tick.
   - Only applicants with a score (`Assessed`) can be ticked.
   - Families see nothing and no email goes out yet.
2. **Publish (admin):** every scored applicant in the session gets a decision:
   - **Ticked → `Offered`.** An **admission number** is reserved (`MSS/{session start year}/####`, never reused).
     - The family gets the **offer email** with the **admission letter PDF** attached: letterhead, admission number, class, session, resumption date, and the principal's **uploaded signature** (Settings → School).
     - The email includes a short welcome guide: accept the offer, the same login becomes the parent portal, fees and online payment, results.
   - **Not ticked → `NotOffered`.** The family gets a kind regret email with no letter.
   - Applicants still waiting for a score aren't touched, so you can publish again later. With nothing to decide: 422 `admission.nothing_to_publish`.
3. **Family responds:**
   - **Accept** → `Accepted`, with an email "the school will complete enrolment".
   - **Decline** → `Withdrawn`.
4. **Enrol (admin):** pick a class arm (optional; it must be for the offered level).
   - Creates the **student** with the reserved admission number and the form's details (sex, DOB, state, LGA, address, medical notes). They're admitted from the session start (or today if it has already started), so they count as **new intake**.
   - Links **guardians** from the form: father, mother and the person who applied. Existing guardians are matched by phone, so siblings join up. The applicant is the primary contact.
   - The family's **applicant login becomes their parent login**, with the same email and password and the Parent role added. The family gets an "enrolment complete, sign in to the parent portal" email.
   - **Fees:** the new-intake items are billed when the bursar runs **Generate invoices** for the term (it's safe to rerun; it only adds the new students), then publishes.
5. **Letters reprint identically:** the date is the publish date, and everything comes from stored data. They're available while the offer stands: Offered, Accepted or Enrolled.
6. **NDPR clean-up (automatic):**
   - 6 months after `NotOffered`, `Withdrawn` or `Missed`, the child's details and documents are deleted. The reference, status and score are kept for statistics.
   - Applicant-only accounts unused for 12 months are deactivated.

## Endpoints
| Method & path | Who | Body / response | Errors |
|---|---|---|---|
| `PUT /api/admissions/decisions` | Admin | `{ applicationIds: Guid[] (≤ 500), decision: "Offer" \| "Decline" \| null }` → `ExamCandidate[]` | 400; 404; 422 `admission.not_assessed` |
| `POST /api/admissions/decisions/publish` | Admin | `{ sessionId }` → `{ offered, notOffered }` | 404; 422 `admission.nothing_to_publish` |
| `POST /api/admissions/applications/{id}/accept` | Applicant, Parent (own) | → `ApplicationDetail` | 404; 422 `admission.no_offer` |
| `POST /api/admissions/applications/{id}/decline` | Applicant, Parent (own) | → `ApplicationDetail` | 404; 422 `admission.no_offer` |
| `GET /api/admissions/applications/{id}/letter.pdf` | Applicant, Parent (own) | PDF | 404 (no offer, or not yours) |
| `GET /api/admissions/applications/{id}/letter-pdf` | Admin, Bursar | PDF | 404 |
| `POST /api/admissions/applications/{id}/enrol` | Admin | `{ classRoomId?: Guid }` → `{ studentId, admissionNumber, application: ApplicationDetail }` | 400 `classRoomId` (wrong level) or a full class; 404; 409 `admission.number_taken`; 422 `admission.not_accepted` |

- **Changed shapes:**
  - `ExamCandidate` has a new `decision: "Offer" | "Decline" | null` (the draft tick).
  - `ApplicationDto` has new `admissionNumber` (set when offered) and `studentId` (set when enrolled).
- **Statuses now in use:** `Offered`, `NotOffered`, `Accepted`, `Enrolled`, `Withdrawn`.

## UI to build
1. **Staff Results page (from part 3):**
   - A checkbox column on the **Passed** tab (and Failed, for discretion) bound to `decision`. Saves via `PUT /decisions`, with "Tick all passed" as a bulk call.
   - **Publish decisions**: confirm "Offer N, not offer M (all other scored applicants)?" → show `{ offered, notOffered }`.
2. **Staff application detail:**
   - Status chips for the new states.
   - **Admission letter (PDF)** when Offered, Accepted or Enrolled.
   - **Enrol** for `Accepted`: a class-arm select filtered to the level, then a success panel linking to the new student record.
3. **Family `/admissions/{id}`:**
   - `Offered`: a celebratory card with the admission number, **Download letter**, **Accept** (primary) and **Decline** (confirm dialog).
   - `Accepted`: "Waiting for the school to complete enrolment".
   - `Enrolled`: "Go to the parent portal". The user may need to sign in again to get the Parent role.
   - `NotOffered`: a kind message with no score.
   - `Withdrawn`: a neutral message.
4. **Routing:** once the user has the Parent role, send them to the parent portal by default. Keep `/admissions` reachable for siblings.
