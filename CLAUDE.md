# Maydel SMS — Engineering Guide

School management system for Maydel Spring Schools, Abuja. Replaces the legacy static site + Firebase portal in `../website` (reference only — do not copy its code; reuse brand, content and the result-template layout).

**Read first:** `docs/00-vision-and-scope.md`, `docs/02-epics-and-features.md` (what to build + acceptance criteria), `docs/01-architecture.md`, `docs/04-security.md`, `docs/05-design-system.md`.

## Stack
- `backend/` — .NET 10, ASP.NET Core minimal APIs, EF Core 10 + PostgreSQL, ASP.NET Core Identity. Clean Architecture + vertical slices (Domain, Application, Infrastructure, Api). Tests: xUnit + Testcontainers.
- `frontend/` — Next.js (App Router) + TypeScript (strict) + Tailwind; BFF pattern for auth (tokens only in httpOnly cookies).
- `docker-compose.yml` — Postgres + API + web for local dev.

## Commands
```bash
cp .env.example .env && docker compose up -d --build    # Postgres + API (demo data); add --profile web once frontend/ exists
docker compose up -d db                                  # Postgres only
pwsh backend/scripts/dev-secrets.ps1                     # once per machine, before `dotnet run` (or dev-secrets.sh)
cd backend && dotnet build && dotnet test               # build also refreshes contracts/openapi.json; tests need Docker
dotnet run --project backend/src/Maydel.Api              # API on http://localhost:5178, docs at /scalar
dotnet tool restore && dotnet ef migrations add <Name> -p backend/src/Maydel.Infrastructure -s backend/src/Maydel.Api -o Persistence/Migrations
dotnet new install backend/templates/slice               # then: dotnet new maydel-slice -n CreateStudent --module Students --kind command --response StudentDetail -o backend/src/Maydel.Application/Students/CreateStudent
cd frontend && npm run dev | npm run lint | npm run typecheck | npm test
```
Demo accounts (Development): `admin@maydel.test`, `teacher@maydel.test`, `bursar@maydel.test`, `parent@maydel.test`, `newteacher@maydel.test` (must change password), `driver@maydel.test` (Bus 1), students `MSS/2026/0001`–`0005`. Password for all: `Demo#Pass2026`.

## Backend conventions (see docs/adr/ADR-004)
- Projects: `Maydel.Domain` ← `Maydel.Application` ← `Maydel.Infrastructure` / `Maydel.Api`.
- **One folder per use case** in Application: `<Module>/<UseCase>/{<UseCase>Command|Query, Handler, Validator}.cs`. Module-shared DTOs live in `<Module>/<Module>Dtos.cs`.
- Commands change state and return `Result`/`Result<T>`. Queries never change state: `AsNoTracking`, projected to DTOs, paginated with `ToPagedAsync`.
- No MediatR. Handlers implement `ICommandHandler<,>`/`IQueryHandler<,>` and are injected into endpoints. Validation, logging and transactions are decorators.
- No repository layer: handlers use `IAppDbContext`. Reused complex queries become named query extensions in the module.
- Expected failures are **returned** (`Error.Validation/NotFound/Forbidden/Conflict/Rule`), never thrown. Exceptions are for unexpected faults only.
- Endpoints live in `Maydel.Api/Endpoints/<Module>Endpoints.cs`, are thin, and return **`TypedResults`** (`Results<Ok<T>, ProblemHttpResult>`) via `result.ToHttpResult()`, declaring error statuses with `.ProducesProblems(400, 403, …)`. Never `Results.Ok`. Problems carry `code` + `traceId` (ADR-005 §4).
- Commands whose failure path must persist side effects implement `INonTransactional` (rare: sign-in, refresh). Everything else gets a transaction that rolls back on a failed `Result`.
- Row-level checks go through `AccessRules` (Application/Common/Access). `backend/drafts/` holds pre-ADR-004 module code: reference only, delete each file once its module is rebuilt.
- Every endpoint group declares an authorization policy. Anonymous endpoints are only allowed under `/api/public`, `/api/auth`, `/api/files` and webhooks.
- **Row-level access is mandatory** for anything student-scoped (parent ↔ child, teacher ↔ class/subject). Add an integration test for the forbidden case.
- Tests: `Maydel.Application.Tests` (handlers, rules, real Postgres), `Maydel.Api.Tests` (HTTP, auth, policies; `ApiFactory` fakes Google). Unique data per test, no DB resets.
- Invariants live in Domain entities (pure, unit-tested). FluentValidation handles request shape.
- No secrets in `appsettings*.json`. Schema changes only through EF migrations; never edit an applied migration.
- Auditing is automatic (`AuditInterceptor`); don't write manual activity logs.

## Frontend conventions
- Server Components by default; `"use client"` only for interactivity.
- Data fetching through `lib/api` server client (adds bearer from cookie). Never call the API from the browser with a token.
- Design tokens & components from `docs/05-design-system.md`; no inline styles, no emoji icons (use lucide-react).
- Every data view has loading, empty and error states; works at 360 px; keyboard-accessible; light + dark.
- Forms: React Hook Form + Zod mirroring server rules; show server problem-details errors per field.

## Definition of Done
See bottom of `docs/02-epics-and-features.md`. In short: authZ (role + row) tested, rules unit-tested, endpoints integration-tested, UI states complete, CI green, docs updated.

## Backend ↔ frontend handoff
A separate frontend session builds alongside. When a milestone's backend is done, write `docs/handoffs/<milestone>.md` (what shipped, endpoints, shapes, error codes, auth/cookie rules, demo data, what the UI should build) and give it to the user to paste into the frontend session. The contract source of truth is `contracts/openapi.json`.

## Commits & pull requests
Conventional commits (`feat(assessment): ...`, `fix(auth): ...`), one story per PR where practical, reference the story id (e.g. `E5.1`).
- **Never push to `main`.** Work on a branch (`feat/e2-school-setup`, `fix/...`), push it, `gh pr create --fill`, merge with **squash** only once CI (`backend`, `frontend`, `secrets`) is green: `gh pr checks --watch && gh pr merge --squash`.
- Once per clone: `git config core.hooksPath .githooks`. The pre-push hook blocks direct pushes to `main`. GitHub-side branch protection needs GitHub Pro for a private repo; until then this hook plus discipline is the guard.
