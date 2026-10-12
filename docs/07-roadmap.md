# 07 — Roadmap & Delivery Plan

Timeboxes assume one full-stack engineer with AI assistance; adjust for team size. Each phase ends with a demo to the school and UAT sign-off.

| Phase | Weeks | Epics | Exit criteria |
|---|---|---|---|
| **0 — Foundation** | 1–2 | E0, E1 (E1.1–E1.4, E1.6), E14.1 | `docker compose up` works; login (password + Google) end-to-end through BFF; RBAC + audit tested; CI green |
| **1 — Replace legacy (MVP)** | 3–8 | E2, E3 (core), E4, E5 (E5.1–E5.6), E6 (results, profile), E7, E8 | All legacy data migrated and reconciled; teachers enter First Term scores; admin publishes; parents view & download report cards; new public site live on the domain; old Firebase portal switched off |
| **2 — School operations** | 9–14 | E9, E10 (manual + Paystack), E11, E12 (incl. targeting & newsletters), E13, E16.1–E16.5 (CBT for CATs), E1.7 MFA, E3.6 promotion | Fees billed and tracked for a full term; daily registers in use; notifications sending; dashboard used by proprietress |
| **3 — Growth** | 15+ | E15 + S/C items | Prioritised with the school after a term of use |

## Phase 1 milestone breakdown
1. **M1.1 School setup & people** — sessions/terms, classes, subjects, students, guardians, staff, logins, credential slips.
2. **M1.2 Assessment** — assessment settings, score grid, comments, preview, publish, PDF report card.
3. **M1.3 Portal** — parent/student sign-in, children switcher, results, profile.
4. **M1.4 Website** — all public pages, CMS for news/gallery/staff, admissions form → inbox.
5. **M1.5 Migration & cut-over** — dry runs on staging, reconciliation sign-off, DNS switch, credential distribution to families.

## Cut-over plan (legacy → new)
1. Freeze edits on Firebase portal (announce to staff).
2. Final export → import → reconciliation report reviewed by admin.
3. Deploy new site; point `maydelspringschools.com` DNS; keep legacy at `legacy.` subdomain read-only for 30 days.
4. Distribute credential slips; help-desk week (WhatsApp line).
5. Decommission Firebase project after 30 days (export archived).

## Risks
| Risk | Mitigation |
|---|---|
| Legacy data inconsistent (names, subject spellings) | Dry-run reports, alias table, manual review queue |
| Staff adoption | Training session per role, short video guides, mobile-friendly score entry |
| Parents without email | Students/parents can sign in with admission no. / phone-based username; SMS credentials |
| Connectivity at school | Autosave drafts in score grid; tolerant retries |
| Single developer bus-factor | Docs (this folder), ADRs, conventions in `CLAUDE.md`, tests |
