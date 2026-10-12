# Maydel SMS

School management system and website for **Maydel Spring Schools, Abuja** — *The Well Spring of Knowledge*.

Replaces the legacy static site and Firebase portal (`../website`) with a secure, maintainable platform: public website + CMS, admissions, student information, staff, assessment & report cards, attendance, fees & online payments, parent/student portal, notifications and audit.

| | |
|---|---|
| Backend | C# · ASP.NET Core 10 · EF Core · PostgreSQL · ASP.NET Core Identity (JWT + rotating refresh, Google sign-in) |
| Frontend | Next.js · TypeScript · Tailwind (BFF session cookies) |
| Ops | Docker Compose (local) · GitHub Actions · managed Postgres |

## Quick start (local)
Prerequisites: Docker Desktop, .NET 10 SDK (to build/test), Node 22 (frontend).

```bash
cp .env.example .env            # dev-only values; set POSTGRES_PORT=5434 if a local Postgres already uses 5432
docker compose up -d --build    # Postgres + API, migrated and seeded with demo data
```
- API docs: http://localhost:5178/scalar · health: http://localhost:5178/health/ready
- Sign in (`POST /api/auth/login`) as `admin@maydel.test` / `Demo#Pass2026`. Other demo accounts are listed in [CLAUDE.md](CLAUDE.md).
- Running the API outside Docker: `pwsh backend/scripts/dev-secrets.ps1` once, `docker compose up -d db`, then `dotnet run --project backend/src/Maydel.Api`.
- Tests: `cd backend && dotnet test` (Docker must be running).

## Documentation
| Doc | Purpose |
|---|---|
| [Vision & scope](docs/00-vision-and-scope.md) | Why, goals, personas, legacy audit |
| [Architecture](docs/01-architecture.md) | System design, modules, tech choices |
| [Epics & features](docs/02-epics-and-features.md) | Backlog with acceptance criteria, DoR/DoD |
| [Domain model](docs/03-domain-model.md) | Entities, invariants, legacy mapping |
| [Security & privacy](docs/04-security.md) | Auth, RBAC matrix, OWASP, NDPA |
| [Design system](docs/05-design-system.md) | Tokens, components, IA, key screens |
| [API conventions](docs/06-api-conventions.md) | REST & error conventions |
| [Roadmap](docs/07-roadmap.md) | Phases, milestones, cut-over |
| [ADRs](docs/adr/) | Architecture decisions |
| [Execute-epic prompt](docs/prompts/execute-epic.md) | Reusable prompt for building each milestone |
| [Handoffs](docs/handoffs/) | Backend → frontend briefs per milestone |
| [API contract](contracts/openapi.json) | Generated OpenAPI document (committed) |
| [CLAUDE.md](CLAUDE.md) | Engineering conventions |

## Status
Phase 0 (foundation): backend done (auth, sessions, RBAC, audit, platform). Frontend in progress, see [handoff](docs/handoffs/phase-0-foundation.md) and the [roadmap](docs/07-roadmap.md).
