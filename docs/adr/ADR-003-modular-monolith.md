# ADR-003 — Modular monolith with PostgreSQL

- **Status:** Accepted (2026-10-12)

## Context
One school, ~1k users, small team, limited ops capacity. Strong consistency needed (scores ↔ reports, invoices ↔ payments).

## Decision
Single API deployable organised into feature modules (see architecture doc §3), one PostgreSQL database, EF Core migrations. Modules write only their own tables. Integration events via an outbox table when asynchronous work is needed (notifications).

## Consequences
- + One transaction per use case, simple deployment, cheap hosting, easy local dev.
- + Clear seams if a module (e.g. notifications) ever needs extracting.
- − Discipline required to keep module boundaries (enforced in review; architecture tests can be added).
