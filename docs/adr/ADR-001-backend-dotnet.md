# ADR-001 — Backend platform: C# / ASP.NET Core 10

- **Status:** Accepted (2026-10-12)
- **Deciders:** Lead engineer

## Context
Legacy system has no backend (Firebase directly from the browser). We need a typed, secure, maintainable backend for a single school (~1k users) with a small team. Candidates: Java 24 / Spring Boot 3, C# / ASP.NET Core 10.

## Decision
ASP.NET Core 10 (LTS, supported to Nov 2028) with EF Core 10 and PostgreSQL.

## Rationale
| Criterion | .NET 10 | Spring Boot |
|---|---|---|
| Identity (hashing, lockout, tokens, external logins) | Built-in ASP.NET Core Identity | Spring Security + custom user store |
| ORM & migrations | EF Core migrations built in | JPA/Hibernate + Flyway/Liquibase |
| API docs, validation, rate limiting, problem details | Built in | Libraries (springdoc, bucket4j…) |
| Memory / cold start (cheap hosting) | Lower | Higher (unless native image) |
| Tooling already installed on dev machine | Yes (SDK 10.0.401) | JDK 24 yes, build tool no |

Both are mature and fully viable; .NET needs fewer moving parts for the same security features.

## Consequences
- Team must be comfortable with C#. Hiring in Abuja/Lagos is viable for both ecosystems.
- Pinned via `global.json` to the 10.0 SDK band.
