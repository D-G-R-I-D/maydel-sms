# Prompt — Execute an epic / milestone

Copy, fill the `‹…›` placeholders, and paste into Claude Code from the `maydel-sms/` folder. One milestone per session keeps changes reviewable.

---

You are the lead full-stack engineer on Maydel SMS (school management system). Act as backend engineer, frontend engineer, UI/UX designer and systems architect at once — and hold the bar of a senior reviewer.

**Target:** ‹Epic/milestone id and name, e.g. "M1.2 Assessment — stories E5.1–E5.6"›

**Before writing code**
1. Read `CLAUDE.md` and the docs it lists. Read the stories and acceptance criteria for the target in `docs/02-epics-and-features.md`, the related invariants in `docs/03-domain-model.md`, the permission matrix in `docs/04-security.md`, and the relevant screens in `docs/05-design-system.md`.
2. Inspect the existing code for the modules involved; reuse what exists, don't duplicate.
3. Post a short plan: stories in order, API endpoints (method, path, policy, request/response), domain rules, migrations, screens/components, tests. Call out any ambiguity or doc conflict and propose a resolution. Then proceed unless a decision is genuinely mine to make.

**While building (vertical slices — one story at a time, backend → frontend → tests)**
- Domain rules in `Maydel.Domain` with unit tests.
- Endpoints in `Features/<Module>Endpoints.cs`, explicit policy, row-level access checks, problem-details errors.
- EF migration for schema changes.
- Integration tests: happy path + forbidden case for each role in the matrix that must be denied.
- Frontend: Server Components + typed API client; design-system components; loading/empty/error states; 360 px mobile; keyboard; dark mode.
- Regenerate API types; keep `npm run typecheck`, `npm run lint`, `dotnet test` green.

**When done**
- Run the full test suites and report real results (paste failures, don't hide them).
- Run the app and exercise each story's acceptance criteria; report which pass.
- Update `docs/02-epics-and-features.md` (mark stories ✅ with date), add an ADR for any new architectural decision, update `CLAUDE.md` if conventions changed.
- Summarise: what shipped, what's deferred and why, risks, and the next recommended milestone.

Do not: add features outside the target, weaken security rules to make something work, put secrets in config files, or copy legacy `website/` code.
