# Handoff → Frontend: E11 part 3 — Entrance exam booking, scores and cut-off (E11.6–7)

> Backend status as of 2026-10-17. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on parts 1–2.

## How it works
1. **Admin opens exam sittings** for the session: date/time, length, venue, Paper or CBT, and seats.
   - A sitting with bookings can't be moved or deleted, and its seats can't drop below the number booked.
   - Add another sitting instead.
2. **The family books** after **submitting** the form.
   - They choose from sittings that are in their window (on or before `examWindowEndsOn`), haven't started, and have free seats.
   - They can **move** the booking until their sitting starts.
   - Server time decides; capacity is enforced, including two families clicking at once (409 `exam_slot.busy`, "try again").
   - Emails: booked confirmation, then a **reminder** about 24 hours before.
3. **Missed:**
   - If the window ends without a booking, the application becomes `Missed` (a job every 15 minutes) and the family is emailed.
   - Staff can also mark a booked applicant **absent**, which also makes it `Missed`.
   - The admin can **reopen** it: a fresh window from today, and the family is emailed the new deadline.
4. **Scores:** admin or teacher enters a percentage (0–100) per applicant → `Assessed`. It can be corrected later.
5. **Results:**
   - The admin sets a **cut-off**. The lists (*took the exam* / *passed* / *failed*) are recomputed from the current cut-off on every read, so nothing changes for families until the admin sends.
   - **Notify passed** emails everyone on the passed list who hasn't been told yet ("you passed the first stage; the school will contact you"), **once each**. Running it again only reaches new passes.
   - Failed applicants get nothing yet: the regret email comes with offers in part 4.
   - CSV export is available.

## Endpoints
| Method & path | Who | Body / response | Errors |
|---|---|---|---|
| `GET /api/admissions/applications/{id}/exam-slots` | Applicant, Parent (own) | `ExamSlotOption[]` `{ id, startsAt, endsAt, venue, mode: "Paper"\|"Cbt", seatsLeft, booked }` | 404 |
| `POST /api/admissions/applications/{id}/book-exam` | Applicant, Parent (own) | `{ slotId }` → `ApplicationDetail` | 400 `slotId` (full / past / outside the window / unknown); 404; 409 `exam_slot.busy`; 422 `admission.submit_first` / `admission.missed` / `admission.exam_started` / `admission.exam_closed` |
| `GET /api/admissions/exam-slots?sessionId` | Staff | `ExamSlotDto[]` `{ id, sessionId, startsAt, durationMinutes, venue, mode, capacity, booked, notes }`, soonest first | |
| `POST /api/admissions/exam-slots` | Admin | `{ sessionId, startsAt (ISO with offset), durationMinutes (15–480), venue, mode, capacity (1–1000), notes? }` → `ExamSlotDto` | 400 (`startsAt` must be in the future, …); 404 |
| `PUT /api/admissions/exam-slots/{id}` | Admin | same → `ExamSlotDto` | 400 `capacity`; 404; 422 `exam_slot.has_bookings` (time or length changed) |
| `DELETE /api/admissions/exam-slots/{id}` | Admin | 204 | 404; 422 `exam_slot.has_bookings` |
| `GET /api/admissions/exam-slots/{id}/candidates` | Staff | `ExamCandidate[]` by surname (attendance sheet / score entry) | 404 |
| `PUT /api/admissions/applications/{id}/exam-score` | Admin, Teacher | `{ score }` → `ExamCandidate` | 400 `score`; 404; 422 `admission.not_booked` |
| `POST /api/admissions/applications/{id}/absent` | Admin, Teacher | → `ExamCandidate` | 404; 422 `admission.not_booked` |
| `POST /api/admissions/applications/{id}/reopen` | Admin | → `ApplicationDetail` | 404; 422 `admission.not_missed` |
| `GET /api/admissions/results?sessionId&classLevelId?` | Admin | `ExamResults` | 404 |
| `GET /api/admissions/results.csv?sessionId&classLevelId?` | Admin | CSV | 404 |
| `PUT /api/admissions/results/cut-off` | Admin | `{ sessionId, cutOffMark: number \| null }` → `ExamResults` | 400 `cutOffMark`; 404 |
| `POST /api/admissions/results/notify-passed` | Admin | `{ sessionId }` → `{ queued }` | 404; 422 `admission.no_cut_off` |

```ts
type ExamCandidate = { applicationId; referenceNumber; childName; classLevel; status; score: number | null; stageOneNotifiedAt: string | null };
type ExamResults = { sessionId; cutOffMark: number | null; taken: ExamCandidate[]; passed: ExamCandidate[]; failed: ExamCandidate[];
  awaitingScore: number; missed: number }; // passed/failed are empty until a cut-off is set
```
- **Changed shapes:**
  - `ApplicationDto` has a new `exam: { slotId, startsAt, endsAt, venue, mode } | null`.
  - `ApplicationDetail` has a new `examScore` (staff views only; always null for families).
  - `AdmissionSettingsDto` has a new `cutOffMark`.
- **Times:** all are UTC instants. Show them in Africa/Lagos time. When creating sittings, send the offset (e.g. `2026-10-19T09:00:00+01:00`).

## UI to build
1. **Family `/admissions/{id}`:**
   - Once `Submitted`: a "Book your entrance exam" card listing sittings (date, time, venue, Paper/CBT, seats left) with a **Book** button.
   - Once `ExamBooked`: show the booking with **Change time** (until it starts) and a note to bring a pencil, pen and eraser and arrive 15 minutes early.
   - `Missed`: "The exam window has closed. Contact the school."
   - Don't show scores to families.
2. **Staff Admissions → Exam sittings (admin):**
   - Session picker, a list with booked/capacity bars, and create/edit/delete (disabled when booked).
   - Per sitting: the **candidates** list, which works as a printable attendance sheet, with an inline **score** input (admin and teacher) and an **Absent** action.
3. **Staff Admissions → Results (admin):**
   - A cut-off input; changing it refreshes the three tabs: Took the exam / Passed / Failed. Also show the awaiting-score and missed counts.
   - **Export CSV**.
   - **Notify passed (N)**: confirm "Email N families that they passed the first stage?". Show `stageOneNotifiedAt` as "Emailed" chips.
4. **Application detail (staff):** a **Reopen** button for `Missed` (admin).
5. **Teacher nav:** teachers need the "Exam sittings" page to enter scores.
