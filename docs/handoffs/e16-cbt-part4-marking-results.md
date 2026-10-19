# Handoff → Frontend: E16 part 4 — Marking, results, grade sheet and analytics (E16.4, E16.5, E16.7)

> Backend status as of 2026-10-19. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on parts 1–3. Part 5 (AI question writing) is last.

## How it works
1. **Results page:** `GET /api/cbt/tests/{id}/attempts` lists **everyone in the test's classes** (plus anyone who sat it and has since moved class) with their state:
   - States: `NotStarted`, `InProgress` or `Submitted`.
   - Times: started and handed in, with `autoSubmitted`.
   - Focus log counts: `blurs`, `takeOvers`.
   - Marks: `autoScore` (computer marks so far), `score` (total once everything is marked; best attempt), and `needsMarking`.
   - Overdue attempts are handed in automatically when this page loads.
2. **Marking (E16.4):**
   - **By student:** `GET /api/cbt/attempts/{id}` shows every answer next to the key or marking guide, the options with `isCorrect` and `chosen`, the computer's mark and the teacher's mark, plus the focus log.
   - **By question:** `GET …/tests/{id}/questions/{questionId}/responses` lists every student's answer to one question, unmarked first. Use it for consistent theory marking.
   - `PUT /api/cbt/attempts/{id}/items/{itemId}/mark { score, comment? }` marks theory or **overrides** the computer (e.g. an accepted spelling). The score must be 0 to the question's marks, with at most 2 dp. The comment is shown to the student after release.
   - Marks are final once results are released (422 `test.released`).
3. **Release (moderation, then release):** `POST …/tests/{id}/release` works only when:
   - the window has **closed** (422 `test.still_open`);
   - nobody is still sitting (422 `test.still_sitting`);
   - every answer has a mark (422 `test.unmarked`, with the count in the message).
   
   After that, students see their score, per-question marks and comments, and parents see it in the portal.
4. **Grade sheet (E16.5)** (only for tests with a component, after release):
   - `GET …/gradebook` **previews** each student: raw score, **scaled** score (`raw ÷ total × component max`, 2 dp, halves up), the value currently in the grade sheet, and a state:
     - `Ready`;
     - `ReportPublished`: never overwritten;
     - `NotInClass`;
     - `NotYourClass`.
   - After the teacher confirms, `POST …/gradebook { studentIds?: null = all ready }` writes only that component. Other components, comments and totals are recalculated, never wiped. It returns `{ pushed, skipped[] }`, and it can be re-run.
   - 422 `test.not_released` / `test.no_component` (practice test).
5. **Analytics (E16.7):** `GET …/tests/{id}/analytics` returns:
   - The best fully-marked attempt per student: mean, median, high and low.
   - A 10-band distribution (0–10% … 90–100%).
   - The mean per class arm.
   - **Per question:** responses, answered, **facility** (average share of the marks, 0–1; low = hard), and **option picks** for distractor analysis. Sorted hardest first within each section.
6. **Students and parents:**
   - `StudentTestDto` has new `resultsReleased`, `score` and `resultAttemptId`.
   - `AttemptPaper` has new `released`, `score`, `totalMarks`, and per item `score` and `comment` (null until released).
   - Parents and students: `GET /api/portal/children/{studentId}/tests` returns released results `{ testId, title, subject, submittedAt, score, totalMarks, percent }`.
   - `TestDetail` has new `resultsReleasedAt` and `gradebookPushedAt`.

## Endpoints (Admin, Teacher with access to the test; otherwise 404)
| Method & path | Body / response | Errors |
|---|---|---|
| `GET /api/cbt/tests/{id}/attempts` | `AttemptRows { testId, totalMarks, resultsReleasedAt, sitting, needsMarking, students: AttemptRow[] }` | 404 |
| `GET /api/cbt/attempts/{id}` | `AttemptReview` | 404 |
| `GET /api/cbt/tests/{id}/questions/{questionId}/responses` | `QuestionResponses` | 404 |
| `PUT /api/cbt/attempts/{id}/items/{itemId}/mark` | `{ score, comment? }` → `AttemptReview` | 400 `score` / `comment`; 404; 422 `attempt.not_submitted` / `test.released` |
| `POST /api/cbt/tests/{id}/release` | → `TestDetail` | 404; 422 as above |
| `GET /api/cbt/tests/{id}/gradebook` | `GradebookPreview { component, componentMax, totalMarks, rows[] }` | 404; 422 `test.not_released` / `test.no_component` |
| `POST /api/cbt/tests/{id}/gradebook` | `{ studentIds?: string[] \| null }` → `{ pushed, skipped: GradebookRow[] }` | 404; 422 |
| `GET /api/cbt/tests/{id}/analytics` | `TestAnalytics` | 404 |
| `GET /api/portal/children/{studentId}/tests` | `ChildTestResult[]` (Parent, Student) | 404 |

## UI to build
1. **Test → Results tab:**
   - A table of students with a state chip, times, auto score, score, a "needs marking" badge, and focus-log icons (blurs and takeovers, with a tooltip).
   - Header counters: sitting and needing marking.
2. **Marking:**
   - **By student:** a split view (questions on the left, the answer + key/guide + a marks input + comment on the right) with Prev/Next student.
   - **By question:** for theory, one card per student answer with a quick marks input. Unmarked first; saves on blur.
3. **Release results:** a button with a confirm. When disabled, explain why (closed? sitting? N unmarked).
4. **Send to grade sheet:** a preview table (raw → scaled, current value, state chip). Confirm "Write N scores to <component>?", then show the skipped list with reasons. Show "Sent on …" from `gradebookPushedAt`.
5. **Analytics tab:**
   - Distribution bar chart, mean/median/high/low tiles, a class arm table.
   - A question table with facility bars (amber < 0.4, red < 0.2) and expandable option-pick bars, the correct one marked.
6. **Student portal:** a released test shows the score and a "View marked paper" link (per-question marks and comments; no keys).
7. **Parent portal:** a child's "Tests" card list (title, subject, score/total, %).
