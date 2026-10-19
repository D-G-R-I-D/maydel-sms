# Handoff → Frontend: E16 part 3 — Students take tests in the portal (E16.3, E16.6)

> Backend status as of 2026-10-18. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on parts 1–2. Marking, releasing results and the gradebook come in part 4.

## How it works
1. A student sees the **published** tests set for their class (`GET /api/student/tests`), each with a state:
   - `Upcoming`: the window hasn't opened yet.
   - `Open`: they can start.
   - `InProgress`: they have an attempt running (`activeAttemptId`).
   - `Done`: handed in.
   - `Missed`: the window closed without an attempt.
2. **Start** creates the paper:
   - Sections stay in order. Questions are shuffled inside each section and options are shuffled, if the test says so, **differently per student**.
   - Random sections draw fresh questions per student.
   - The **deadline** is the earlier of start + length and the window's close.
   - The response includes a **`sessionToken`**.
3. **Autosave:** send one answer per question as it changes (`PUT …/items/{itemId}`). Each save returns `remainingSeconds` from the **server clock**.
   - Use `serverNow`, `deadlineAt` and `remainingSeconds` for the countdown, not the device clock.
   - Answer shapes: Objective `optionIds: [one]`, MultiSelect `optionIds: [...]`, TrueFalse `trueFalse`, gap/short/theory `text` (≤ 5000 characters).
4. **One live session:** starting again (another tab, a refresh after a dropped connection, another device) **resumes the same attempt**, with a new token.
   - The old token then gets **409 `attempt.other_session`**. Show "This test is open somewhere else. Continue here?", then call `start` again to take over.
   - Takeovers are logged.
5. **Focus log:** send `Blur` when the tab is hidden or loses focus and `Focus` when it's back (`POST …/events`). Teachers see these in part 4.
6. **Submit:** objective parts are marked immediately, but **no score is shown** until the teacher releases results (part 4).
7. **Time up:** saves are accepted up to 15 seconds after the deadline (requests already in flight). After that the attempt is **handed in automatically** (`autoSubmitted: true`). Any later save gets 422 `attempt.submitted`. Call submit yourself when the countdown hits 0.
8. Parents can't sit tests (403); only `Student` accounts can. Answer keys and marking guides are never sent to students.

## Endpoints (Student only)
| Method & path | Body / response | Errors |
|---|---|---|
| `GET /api/student/tests` | `StudentTestDto[]`, in-progress first, then open, upcoming, finished | 403 |
| `POST /api/student/tests/{id}/start` | → `AttemptPaper` (with `sessionToken`) | 404; 409 `attempt.starting` (double click; refresh); 422 `test.not_open` / `test.closed` / `test.no_attempts` |
| `GET /api/student/attempts/{id}` | → `AttemptPaper` (no token; use it to show the read-only paper after submitting) | 404 |
| `PUT /api/student/attempts/{id}/items/{itemId}` | `{ sessionToken, optionIds?, trueFalse?, text? }` → `{ itemId, savedAt, remainingSeconds }` | 400 `optionIds` / `text` / `itemId`; 404; 409 `attempt.other_session`; 422 `attempt.submitted` / `attempt.time_up` |
| `POST /api/student/attempts/{id}/events` | `{ sessionToken, kind: "Blur" \| "Focus" }` → 204 | 400; 409; 422 |
| `POST /api/student/attempts/{id}/submit` | `{ sessionToken }` → `AttemptPaper` | 404; 409 `attempt.other_session` |
| `GET /api/student/attempts/{id}/items/{itemId}/image` | PNG | 404 |

```ts
type StudentTestDto = { id; title; subject; instructions; durationMinutes; opensAt; closesAt; questionCount; totalMarks; maxAttempts;
  attemptsUsed; state: "Upcoming"|"Open"|"InProgress"|"Done"|"Missed"; activeAttemptId: string | null };
type AttemptPaper = { id; testId; title; instructions; status: "InProgress"|"Submitted"; startedAt; deadlineAt; serverNow; remainingSeconds;
  sessionToken: string | null; sections: { index; title; instructions; kind; marksPerQuestion }[];
  items: { id; order; section /* index */; kind; text; imageUrl: string | null; marks; options: { id; text }[];
    answerOptionIds: string[]; answerTrueFalse: boolean | null; answerText: string | null; answered: boolean }[];
  answered; submittedAt; autoSubmitted };
```
- Also: unpublishing a test is now blocked once any student has started it (422 `test.started`).

## UI to build (Student portal: "Tests")
1. **List:** cards with subject, title, window ("Opens Mon 9:00", "Closes in 2h"), length, questions and marks, and attempts. The primary action depends on state: Start, Continue, or nothing.
2. **Before starting:** an instructions screen ("45 minutes, 30 questions; your answers save as you go; don't close the tab").
3. **Test screen:**
   - A sticky header with a countdown (server-based, amber under 5 minutes, red under 1) and "N of M answered".
   - A question navigator grid with answered and unanswered states. Show the section title and instructions when a section starts.
   - Question types: radio (Objective), checkboxes (MultiSelect), True/False buttons, an inline input for the gap (render `___` as the input), and a textarea for short answer and theory.
   - Pictures via the BFF.
   - Debounced autosave (~800 ms) with a "Saved" tick. Retry on network errors; on 409, show the takeover prompt.
   - `visibilitychange` / `blur` → Blur, focus → Focus.
4. **Submit:** a confirm listing unanswered questions. Then a "Handed in. Your teacher will release the results." screen. At 0, call submit automatically and show the same screen.
5. **Accessibility:** keyboard-operable, readable at 360 px, timer announced politely at 5 and 1 minute.
