# Handoff → Frontend: E16 part 1 — Question bank (E16.1)

> Backend status as of 2026-10-18. Contract: `contracts/openapi.json` (run `npm run api:types`). The first of five CBT parts: next come tests built from sections, students taking tests, marking and gradebook, then AI question writing.

## How it works
- **One bank per subject and class level** (e.g. Mathematics · Year 3), shared by everyone who teaches it.
- **Who can write questions:**
  - A teacher who teaches that subject in any arm of that level, or who form-teaches an arm of that level (any subject).
  - Admins: everything.
  - Others get **403** for lists and **404** for a single question, so they can't tell whether it exists.
- **Question kinds:**
  - `Objective`: 2–8 options, **exactly one** correct.
  - `MultiSelect`: 2–8 options, at least one correct. Full marks only for exactly the right set.
  - `TrueFalse`: `trueFalseAnswer` required.
  - `FillInTheGap`: the text has **one** gap written as `___` (3+ underscores), plus at least one accepted answer. Marking ignores case, extra spaces and trailing punctuation.
  - `ShortAnswer`: optional accepted answers (auto-marked when given; otherwise the teacher marks it) and an optional marking guide.
  - `Theory`: a **marking guide** is required; the teacher marks it.
  - `isAutoMarked` in the DTO tells you which questions the computer marks.
- **Tags and pictures:**
  - Tags: a `topic` (free text, ≤ 80 characters; reuse suggestions from `/topics`) and a `difficulty` of `Easy` / `Medium` / `Hard` (default Medium).
  - Pictures: one per question, re-encoded to PNG (≤ 1200 px). Served only to staff who can see the question; students will get them through their attempt in part 3.
- **Editing and deleting:**
  - Edits keep option ids when you send them back (`options[].id`), so answers already given still match. Options without an `id` are new.
  - **Delete archives** the question: it leaves the bank, but tests that used it keep it.
- Answer keys are staff-only; students will never receive them.

## Endpoints (Admin, Teacher)
| Method & path | Body / response | Errors |
|---|---|---|
| `GET /api/cbt/scopes` | `AuthoringScope[]` `{ subjectId, subject, classLevelId, classLevel, questions }`: what the caller can write for (admins: every subject offered at each level) | |
| `GET /api/cbt/topics?subjectId&classLevelId` | `string[]` A–Z | 403 |
| `GET /api/cbt/questions?subjectId&classLevelId&kind?&topic?&difficulty?&search?&page&pageSize` | `PagedResult<QuestionDto>`, newest first | 403 |
| `GET /api/cbt/questions/{id}` | `QuestionDto` | 404 |
| `POST /api/cbt/questions` | `QuestionInput` → `QuestionDto` | 400 (`text`, `options`, `trueFalseAnswer`, `acceptedAnswers`, `markingGuide`, `topic`, `subjectId`); 403 |
| `PUT /api/cbt/questions/{id}` | `QuestionInput` → `QuestionDto` | 400; 403; 404 |
| `DELETE /api/cbt/questions/{id}` | 204 (archived) | 404 |
| `PUT /api/cbt/questions/{id}/image` | multipart `file` → `QuestionDto` | 400 `file`; 404 |
| `DELETE /api/cbt/questions/{id}/image` | → `QuestionDto` | 404 |
| `GET /api/cbt/questions/{id}/image` | PNG (private cache) | 404 |

```ts
type QuestionInput = { subjectId; classLevelId; kind: "Objective"|"MultiSelect"|"TrueFalse"|"FillInTheGap"|"ShortAnswer"|"Theory";
  text: string; options?: { id?: string; text: string; isCorrect: boolean }[]; trueFalseAnswer?: boolean;
  acceptedAnswers?: string[]; markingGuide?: string; topic?: string; difficulty?: "Easy"|"Medium"|"Hard" };
type QuestionDto = QuestionInput & { id; subject; classLevel; hasImage: boolean; options: { id; text; isCorrect }[];
  source: "Manual"|"Ai"; isAutoMarked: boolean; createdBy: string | null; createdAt; updatedAt: string | null };
```

## UI to build (Teacher and Admin: "Question bank")
1. **Landing:** cards from `/scopes` (subject · level · N questions).
2. **Bank list:**
   - Filters: kind, topic (from `/topics`), difficulty, search.
   - Each row: a kind chip, the first line of the text, topic, difficulty, an "Auto-marked" or "Teacher-marked" badge, and the author.
   - Empty state: "No questions yet. Add the first one."
3. **Question editor** (one form that adapts to the kind):
   - Objective / multi-select: an option list with add, remove and reorder, plus "correct" toggles (a radio for Objective, checkboxes for MultiSelect).
   - True/false: a toggle.
   - Gap: show a hint "type ___ where the gap is" and preview the sentence with a blank, plus chips for accepted answers.
   - Short answer: chips for accepted answers (optional) and a marking guide (optional).
   - Theory: marking guide (required).
   - Also: topic combobox, difficulty, picture upload with preview (blob URL via the BFF).
   - Show server field errors inline.
   - **Preview as student**: hide the keys.
4. **Archive** with a confirm ("Tests that already use it keep it").
5. **Nav:** "Question bank" for Teacher and Admin.
