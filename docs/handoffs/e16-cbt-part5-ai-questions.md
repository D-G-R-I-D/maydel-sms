# Handoff → Frontend: E16 part 5 — AI question writing and quotas (E16.8, E16.9)

> Backend status as of 2026-10-19. Contract: `contracts/openapi.json` (run `npm run api:types`). Completes E16.

## How it works
1. **The teacher describes the request** for a subject and level they teach:
   - Assessment type: `Test`, `Cat` or `Exam`.
   - Question **kinds**: any of the six.
   - **How many** (1–40), with a **difficulty mix** (easy + medium + hard must add up to the count).
   - Optional **guidance** (topics, curriculum notes; ≤ 4000 characters).
   - Up to 5 **files**: the scheme of work, notes as a **PDF** (≤ 5 MB), or **photos** of notes (JPEG/PNG/WebP ≤ 12 MB; re-encoded, EXIF stripped). They need files or guidance.
   - Files are stored privately.
2. **Claude writes draft questions** (server-side, model `claude-opus-5-5`, JSON checked against a schema), with answer keys and, for theory, marking guides.
   - It writes **only from the uploaded material and the chosen subject and class**.
   - **No student data is ever sent.** The request contains only the subject, class and assessment names, the teacher's files and guidance, and (when regenerating) the earlier questions to avoid.
3. Each version comes back as `questions[]`, each with a `problem`: null if it would pass the bank's rules, otherwise a plain-words reason (e.g. "Mark exactly one option as correct") that the teacher must fix before saving.
4. **Regenerate** up to **5 times** per request. Each new version avoids the earlier ones (422 `ai.regenerations` after that). The teacher can open **any version**.
5. **Save:** the teacher edits freely (text, options, keys, guide, topic, difficulty) or adds their own, then saves the chosen questions → they go into the bank with `source: "Ai"`.
   - All-or-nothing; errors are keyed `questions[i].<field>`.
   - From there they're used in tests like any other question (parts 2–4).
6. **Quota (E16.9):**
   - Each **successful** generation or regeneration uses 1 from the teacher's **own** monthly allowance (calendar month, Abuja time). Failures and refusals are free.
   - Allowance: a default for everyone, which admins can **override per teacher**. One teacher's use never reduces another's.
   - There's also a **school-wide cap**.
   - Show the remaining allowance before generating (`GET /ai/quota`). Over the limit: 422 `ai.quota` / `ai.school_quota`. Switched off: 422 `ai.disabled`.
7. **Not configured:** if the server has no AI key, `quota.available` is false and generating returns 422 `ai.unavailable`. Show "AI question writing isn't set up yet". If the AI declines or fails, the version has `status: "Failed"` with a `message` to show; nothing is charged.

## Endpoints
| Method & path | Who | Body / response | Errors |
|---|---|---|---|
| `GET /api/cbt/ai/quota` | Admin, Teacher | `{ available, enabled, allowance, used, remaining, schoolCap, schoolUsed, resetsAt }` | |
| `POST /api/cbt/ai/generations` | Admin, Teacher | **multipart**: `subjectId`, `classLevelId`, `termId?`, `assessmentType`, `kinds` (comma-separated or repeated), `count`, `easy`, `medium`, `hard`, `guidance?`, `files` (repeat) → `AiGenerationDto` | 400 (`kinds`, `count`, `easy`, `files`, `guidance`, `assessmentType`, `subjectId`); 403; 422 `ai.quota` / `ai.school_quota` / `ai.disabled` / `ai.unavailable` |
| `POST /api/cbt/ai/generations/{id}/regenerate` | owner | → `AiGenerationDto` | 404; 422 `ai.regenerations` + quota codes |
| `GET /api/cbt/ai/generations` | owner | `AiGenerationSummary[]` (last 50) | |
| `GET /api/cbt/ai/generations/{id}` | owner | `AiGenerationDto` | 404 |
| `POST /api/cbt/ai/generations/{id}/save` | owner | `{ questions: DraftInput[] }` → `QuestionDto[]` | 400 `questions[i].…`; 403; 404 |
| `GET /api/cbt/ai/settings` / `PUT …` | Admin | `{ available?, enabled, defaultMonthlyGenerations (0–1000), schoolMonthlyGenerations (0–100000) }` | 400 |
| `PUT /api/cbt/ai/allowances/{userId}` | Admin | `{ monthlyGenerations: number \| null }` (null = back to default) → `AiUsageRow` | 400; 404 |
| `GET /api/cbt/ai/usage?year&month` | Admin | `{ from, to, schoolCap, schoolUsed, inputTokens, outputTokens, teachers: { userId, name, allowance, customAllowance, used, inputTokens, outputTokens }[] }` | |

```ts
type GeneratedQuestion = { kind; text; options: { text; isCorrect }[]; trueFalseAnswer: boolean | null; acceptedAnswers: string[];
  markingGuide: string | null; topic: string | null; difficulty: "Easy"|"Medium"|"Hard" };
type AiGenerationDto = { id; subjectId; subject; classLevelId; classLevel; assessmentType: "Test"|"Cat"|"Exam"; kinds; count; easy; medium; hard;
  guidance; files: string[]; versions: { id; number; createdAt; status: "Ready"|"Failed"; message: string | null;
    questions: { index; question: GeneratedQuestion; problem: string | null }[] }[]; regenerations; canRegenerate; createdAt; quota: AiQuotaDto };
type DraftInput = { kind; text; options?: { text; isCorrect }[]; trueFalseAnswer?; acceptedAnswers?; markingGuide?; topic?; difficulty };
```

## UI to build
1. **Question bank → "Write with AI"** (Teacher, Admin):
   - Show the allowance chip ("12 of 30 left this month") from `/ai/quota`. Disable it with a reason when unavailable, disabled or used up.
   - Form: scope (from `/scopes`), assessment type, kind chips, count, a difficulty mix (three steppers that must add up), a guidance textarea, and a file drop (PDF/photos, camera on mobile).
   - The note "Only your material is sent to the AI. No student information."
   - A generating state (it can take 30–90 seconds); don't time out the BFF too early.
2. **Review screen:**
   - Version tabs (v1…v6) with **Regenerate (N left)**.
   - Each question is an editable card using the same editor as the bank, with a problem banner and a checkbox to include it. "Add my own question" is there too.
   - **Save selected to bank**, then link to the bank filtered to these.
   - Show a failed version's message.
3. **Recent requests** list.
4. **Admin → Settings → AI:**
   - An on/off switch, the default allowance and the school cap.
   - **Usage dashboard** for the month: a school bar (used / cap), and a teacher table with used / allowance, tokens, and an inline allowance override (empty = default).
