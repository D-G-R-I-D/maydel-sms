# Handoff → Frontend: E16 part 2 — Tests and exams built from sections (E16.2, E16.2a)

> Backend status as of 2026-10-18. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on part 1 (question bank). Students taking tests comes in part 3.

## How it works
1. **Header:**
   - Title, instructions, subject, class level, term, and an optional **assessment component** it feeds (e.g. CAT / 30); none means a practice test.
   - The **arms** that sit it, sitting length (5–300 minutes), **open/close window** (it must fit the sitting), attempts (1–5), and shuffle questions/options (on by default).
   - Teachers can only choose arms at that level where they teach the subject or are the form teacher.
2. **Sections:** each section holds **one kind** of question, with its own **marks per question** and instructions.
   - `Fixed`: pick questions from the bank, in order.
   - `Random`: each student draws `randomCount` questions from the bank for that kind, optionally filtered by topic and/or difficulty. `availableInBank` shows how many match.
   - A question can't be used twice. Each test has at most 10 sections and 200 questions.
3. **Totals and scaling:**
   - Totals: each section's marks are questions × marks per question, and they add up to the test total.
   - Scaling: when a component is set, `scaling` shows e.g. 100 marks → Exams / 40 (factor 0.4). Scores are scaled as `score ÷ total × component max`, rounded like the report sheet (2 dp, halves up), when marks go to the gradebook (part 4).
4. **Readiness:** `problems` lists in plain words everything blocking publish (no sections, empty sections, removed questions, not enough bank questions for a draw, bad window). It's empty when the test is ready.
5. **Publish:**
   - Publishing locks the test (422 `test.locked` on edits), and its classes see it inside the window (part 3).
   - **Unpublish** returns it to draft (blocked once students have started, from part 3).
   - Drafts can be deleted.

## Endpoints (Admin, Teacher)
| Method & path | Body / response | Errors |
|---|---|---|
| `GET /api/cbt/tests?termId&subjectId?&classLevelId?&status?` | `TestSummary[]` (only tests you can manage), by opening time | |
| `GET /api/cbt/tests/{id}` | `TestDetail` | 404 |
| `POST /api/cbt/tests` | `TestInput` → `TestDetail` | 400 (`title`, `durationMinutes`, `closesAt`, `maxAttempts`, `classRoomIds`, `termId`, `componentId`, `subjectId`); 403 |
| `PUT /api/cbt/tests/{id}` | `TestInput` → `TestDetail` (draft only; subject/level fixed once it has sections) | 400; 404; 422 `test.locked` |
| `PUT /api/cbt/tests/{id}/sections` | `{ sections: SectionInput[] }` → `TestDetail` (replaces all, in order) | 400 `sections`; 404; 422 `test.locked` |
| `POST /api/cbt/tests/{id}/publish` | → `TestDetail` | 404; 422 `test.not_ready` (first problem in the message) / `test.published`; 400 `closesAt` (window already closed) |
| `POST /api/cbt/tests/{id}/unpublish` | → `TestDetail` | 404; 422 `test.draft` / `test.started` |
| `DELETE /api/cbt/tests/{id}` | 204 (draft only) | 404; 422 `test.locked` |

```ts
type TestInput = { title; instructions?; subjectId; classLevelId; termId; componentId?: string | null; classRoomIds: string[];
  durationMinutes: number; opensAt: string; closesAt: string; maxAttempts?: number; shuffleQuestions?: boolean; shuffleOptions?: boolean };
type SectionInput = { title; instructions?; kind: QuestionKind; marksPerQuestion: number; mode: "Fixed" | "Random";
  questionIds?: string[]; randomCount?: number; topic?: string; difficulty?: "Easy" | "Medium" | "Hard" };
type TestDetail = TestInput & { id; subject; classLevel; term; classRooms: { id; name }[]; status: "Draft" | "Published"; publishedAt;
  sections: { id; order; title; instructions; kind; marksPerQuestion; mode; questionCount; marks; randomCount; topic; difficulty;
    questions: { id; kind; text /* ≤ 200 chars */; topic; difficulty; hasImage; isArchived }[]; availableInBank: number | null }[];
  questionCount; totalMarks; scaling: { componentId; component; componentMax; totalMarks; factor } | null; problems: string[]; canEdit: boolean };
```
- Components come from `GET /api/assessment/settings` (`components`, active ones only). Arms come from `GET /api/academics/classrooms` (filter by level).
- Times are instants; show them in Africa/Lagos time and send them with an offset.

## UI to build (Teacher and Admin: "Tests & exams")
1. **List per term:** title, subject · level, component chip, arms, window, length, question count and total marks, and a status badge. Filters: subject, level, status.
2. **Builder, Header step:** subject + level (from `/api/cbt/scopes`), term, component (or "Practice"), arms (multi-select), date/time window, length, attempts, shuffle toggles.
3. **Builder, Sections step:**
   - Add a section: title, kind, marks per question, Fixed or Random.
   - Fixed: a **bank picker** filtered to that kind (reuse the bank list with checkboxes), with drag to reorder.
   - Random: count + topic/difficulty filters, showing "N available".
   - A running **totals panel** shows per-section marks, the total, and the scaling line (e.g. "100 marks → Exams /40 (×0.40)").
4. **Readiness checklist** from `problems`. **Publish** is enabled only when it's empty and confirms "Students in N classes will see it from …".
5. **Published view:** read-only with **Unpublish** (and later, results). Drafts get **Delete**.
