# 02 — Epics, Features & Acceptance Criteria

Priority: **M** = Must (v1), **S** = Should (v1 if time), **C** = Could (later). Phase refers to [07-roadmap](07-roadmap.md).
Status marks: ✅ done · 🟡 partly done (see note) · no mark = not started.
Story format: *As a ‹role›, I want ‹capability› so that ‹outcome›.* Acceptance criteria (AC) are testable.

| ID | Epic | Phase | Priority |
|---|---|---|---|
| E0 | Platform foundation | 0 | M |
| E1 | Identity & access | 0 | M |
| E2 | School setup | 1 | M |
| E3 | Student information system | 1 | M |
| E4 | Staff management | 1 | M |
| E5 | Assessment & report cards | 1 | M |
| E6 | Parent & student portal | 1 | M |
| E7 | Public website & CMS | 1 | M |
| E8 | Legacy data migration | 1 | M |
| E9 | Attendance | 2 | M |
| E10 | Fees & payments | 2 | M |
| E11 | Admissions pipeline | 2 | S |
| E12 | Communication, notifications & newsletters | 2 | M |
| E13 | Dashboards & analytics | 2 | S |
| E14 | Compliance, audit & operations | 0–2 | M |
| E15 | Timetable, messaging, homework | 3 | C |
| E16 | Online tests & exams (CBT) | 2–3 | S |

---

## E0 — Platform foundation
Goal: a repo anyone can clone and run in one command, with CI guarding quality.

- **E0.1** 🟡 *Backend + compose done 2026-10-12; `web` service waits for `frontend/`.* Monorepo (`backend/`, `frontend/`, `docs/`), `docker compose up` starts Postgres + API + web with demo data. *AC:* fresh clone → working login in ≤ 10 min following README.
- **E0.2** 🟡 *Workflow written 2026-10-12 (backend, frontend-when-present, gitleaks); not yet run: needs the GitHub repo + branch protection.* CI: build, lint, unit + integration tests (Testcontainers), frontend type-check/lint/test, Playwright smoke. *AC:* PRs cannot merge red.
- **E0.3** ✅ *2026-10-12.* Config & secrets via environment; `appsettings` holds no secrets. *AC:* app refuses to start without `Jwt:SigningKey` ≥ 32 chars.
- **E0.4** ✅ *2026-10-12 (API). Error tracking service (Sentry) deferred to deployment.* Observability: structured logs, `/health`, error tracking. *AC:* an unhandled exception yields a 500 problem response with a trace id and a log entry; no stack trace leaks to clients.
- **E0.5** 🟡 *Contract exported to `contracts/openapi.json` + CI staleness check 2026-10-12; TS client generation is frontend work.* OpenAPI document + generated TypeScript client. *AC:* changing a DTO and regenerating breaks the frontend type-check until updated.

## E1 — Identity & access
- **E1.1** 🟡 *API ✅ 2026-10-12; UI pending (frontend).* *As any user, I sign in with username (admission no. for students, email for staff/parents) and password.* AC: generic error on bad credentials (no user enumeration); lockout after 5 failures for 15 min; rate limit 10/min/IP.
- **E1.2** 🟡 *API ✅ 2026-10-12; UI pending (frontend).* *As a parent or staff member, I can "Continue with Google".* AC: Google ID token verified server-side (audience, signature, `email_verified`); only signs in to an **existing** account with that email; never auto-creates accounts; students cannot use Google; first use links the Google subject, later a different Google account for the same email is rejected.
- **E1.3** 🟡 *API ✅ 2026-10-12; BFF cookies/middleware pending (frontend).* Sessions: 15-min JWT + 14-day rotating refresh token in httpOnly cookies. AC: refresh token reuse revokes the token family; logout revokes; password change signs out other devices; deactivating a user invalidates their access within one request (security-stamp check).
- **E1.4** 🟡 *API ✅ 2026-10-12 (enforced server-side, ADR-005 §2); UI routing pending (frontend).* First-login password change for staff-issued temporary passwords. AC: users with `MustChangePassword` are routed to change-password before any other page.
- **E1.5** Password reset: staff resets via admin (temporary password); staff/parents with email can self-serve via emailed link (phase 2). AC: reset tokens single-use, 30-min expiry.
- **E1.6** ✅ *2026-10-12: role policies, secure-by-default fallback, `AccessRules` row-level rules tested against Postgres. Each Phase 1 endpoint adds its own forbidden-case HTTP test.* RBAC: roles Admin, Teacher, Bursar, Parent, Student + row-level rules (see [04-security](04-security.md) matrix). AC: integration tests prove a parent cannot read another family's child, a teacher cannot grade a subject/class not assigned.
- **E1.7 (S)** TOTP MFA required for Admin and Bursar.

## E2 — School setup
- **E2.1** 🟡 *API ✅ 2026-10-13; UI pending.* Sessions & terms with dates, mid-term break, one current term. AC: setting a term current unsets the previous one atomically.
- **E2.2** 🟡 *API ✅ 2026-10-13; UI pending.* Class levels (Creche → Year 12, by section) and arms (e.g. Year 3 A/B), capacity, form teacher.
- **E2.3** 🟡 *API ✅ 2026-10-13; UI pending.* Subjects catalogue and per-class subject offering with assigned subject teacher.
- **E2.4** 🟡 *API ✅ 2026-10-13; UI pending.* Assessment configuration: components (name, max) and grade bands. Default (agreed 2026-10-12): Project 10 · Homefun 5 · Class Fun 5 · Attendance 10 · CAT (Continuous Assessment Test) 30 · Exams 40 = 100. AC: active components must total 100; bands start at 0 with unique minima; changing config never alters already-published reports.
- **E2.5 (S)** 🟡 *API ✅ 2026-10-14 (A–E key and 14 traits from the school sheet, clubs ≤ 6, form teacher/admin, locked on publish, on the PDF; never affects scores); UI pending.* Affective & psychomotor traits rated on the report card, plus clubs & activities — standard on Nigerian report cards.
- **E2.6** 🟡 *API ✅ 2026-10-14 (editable details, PNG logo (public) and principal signature (admin-only) on the report PDF); UI pending.* School profile: name, motto, logo, address, principal's signature image (used on report cards & receipts).

## E3 — Student information system
- **E3.1** 🟡 *API ✅ 2026-10-13 incl. photos (re-encoded, private; ADR-006); UI pending.* Create/edit student with biodata (name, DOB, sex, state/LGA, address, house, medical notes, photo). AC: admission number auto-generated `MSS/{year}/{seq}`, unique.
- **E3.2** 🟡 *API ✅ 2026-10-13 (reuse by normalised phone); UI pending.* Guardians: many-to-many with relationship, primary contact, authorised pick-up. AC: adding a guardian with an existing phone reuses that guardian (siblings share one parent account).
- **E3.3** 🟡 *API ✅ 2026-10-13; UI pending.* Issue/reset logins for students and guardians; temporary password shown once. AC: never retrievable later; reset revokes sessions.
- **E3.4** 🟡 *API ✅ 2026-10-13 (performance at 2k students not yet measured); UI pending.* Search & filter (name, admission no., class, status) with pagination. AC: < 300 ms for 2k students.
- **E3.5** 🟡 *API ✅ 2026-10-13; UI pending.* Lifecycle: Active, Suspended, Withdrawn, Graduated. AC: non-active students lose portal access; history retained.
- **E3.6** End-of-session promotion wizard: promote class → next level, with per-student exceptions (repeat/withdraw); Year 12 → Graduated. AC: preview before commit; single transaction; audited.
- **E3.7 (S)** Documents per student (birth certificate, previous school report) — private storage.
- **E3.8 (S)** Bulk import from Excel template with validation report.

## E4 — Staff management
- **E4.1** 🟡 *API ✅ 2026-10-13 (logins, roles, self-lockout guard, admin password reset); UI pending.* Staff records with role (Admin/Teacher/Bursar), creating a login with temporary password. AC: email unique; admins cannot demote/deactivate themselves.
- **E4.2** 🟡 *API ✅ 2026-10-13 (form teacher on the class, subject teacher per class subject); UI pending.* Assign form teacher per class and subject teacher per class-subject.
- **E4.3** Public profile toggle (photo, title, bio, order) feeding the website Staff page.

## E5 — Assessment & report cards  ★ core value
- **E5.1** 🟡 *API ✅ 2026-10-13 (row-level: subject/form teacher; partial autosave); UI pending.* *As a subject teacher, I enter scores for my subject for a whole class in one grid.* AC: keyboard-friendly grid (tab/enter), per-component max enforced client & server, total/grade computed live, autosave draft, works on a tablet.
- **E5.2** 🟡 *Lock ✅ 2026-10-13; publish/unpublish come with E5.5.* Scores lock once the student's report is published; unpublish (admin) unlocks. AC: API returns 422 on edit of published scores.
- **E5.3** 🟡 *API ✅ 2026-10-14; UI pending.* *As a form teacher, I write a comment per student; as admin/principal, a principal's comment.*
- **E5.4** 🟡 *API ✅ 2026-10-14 (attendance entered manually until E9); UI pending.* Report preview per student showing components, total, grade, remark, subject class highest/lowest/average, overall average, class average, position (competition ranking 1,2,2,4), attendance (days opened/present/absent from register).
- **E5.5** 🟡 *API ✅ 2026-10-14; parent email on publish ✅ 2026-10-15 (E12.3); UI pending.* Publish per class & term (admin). AC: freezes averages, positions, attendance on the report; parents notified (E12); idempotent re-publish recalculates.
- **E5.6** 🟡 *Default template ✅ 2026-10-14 (QuestPDF, one A4 page ≤ 15 subjects, draft watermark, term comparison); configurable templates and versioning pending.* PDF report card generated from a **school-configurable report template** (layout, orientation, sections on/off and order: biodata, components, class stats, traits, attendance, comments, grading key, next-term date; logo, crest, signature images). Default template reproduces the school's current Excel result sheet. AC: admin previews a template with sample data before activating it; published reports keep the template version they were published with; A4 print fits one page for ≤ 15 subjects.
- **E5.6b** ✅ *2026-10-14: class PDF, one page per student.* Bulk download: all report cards for a class as one PDF / ZIP (for printing).
- **E5.7 (S)** Class broadsheet (all students × all subjects) export to Excel.
- **E5.8 (S)** ✅ *2026-10-14: annual average plus per-subject and overall comparison with earlier published terms.* Cumulative/annual result (average of three terms) on third-term report.
- **E5.9 (C)** Fee-debt result withholding (school policy switch).

## E6 — Parent & student portal
- **E6.1** 🟡 *API: children + latest published result ✅ 2026-10-14 (fees, attendance, notices later); UI pending.* Dashboard: children switcher (parent), current class, latest published result summary, fee balance, attendance %, latest notices.
- **E6.2** 🟡 *API ✅ 2026-10-14 (published only; other families and drafts are 404; PDF); UI pending.* Results: list of published terms → report card view + PDF download. AC: drafts never visible (tested).
- **E6.3** 🟡 *API ✅ 2026-10-15 (invoices, payments, balance, overdue, invoice and receipt PDFs; Pay online with E10.5); UI pending.* Fees: invoices, payments, receipts (PDF), outstanding balance; Pay online button (E10.5).
- **E6.4** Attendance: term calendar with absences/lates.
- **E6.5** Profile: biodata (read-only), guardians, request-correction link.
- **E6.6** Notices & events relevant to the audience.
- **E6.7** PWA: installable, phone-first, works on 3G (pages < 200 KB JS on first load).

## E7 — Public website & CMS
Rebuild the existing design in Next.js with improved IA, performance and accessibility — not a copy.
- **E7.1** Pages: Home, About (story, vision/mission, values, founder), Academics/Programmes (Creche → SSS, curriculum), Admissions (process, apply form, fees guide, calendar), Parents (hours, transport, CCTV/security, uniform, meals, clubs, policies, FAQ), Staff, Gallery, News & Events, Contact (form, map, WhatsApp).
- **E7.2** Apply form → creates Admission Application (E11) with reference number shown to the parent. AC: spam protection (rate limit + honeypot; Turnstile if abused).
- **E7.3** News, events, gallery, staff profiles managed in admin (no code changes to publish).
- **E7.4** SEO: per-page metadata, Open Graph, `sitemap.xml`, `robots.txt`, JSON-LD `School` schema, canonical URLs.
- **E7.5** Accessibility WCAG 2.2 AA: keyboard nav, focus states, contrast, alt text, reduced-motion. AC: axe has zero serious violations.
- **E7.6** Light/dark theme respecting system preference.
- **E7.7** Performance: images via `next/image` (AVIF/WebP, responsive), fonts self-hosted via `next/font`. AC: Lighthouse mobile ≥ 90.

## E8 — Legacy data migration
- **E8.1** Export Firestore `students` + `adminActivity` (admin SDK script run by the school).
- **E8.2** Importer: maps students, guardians (from guardian fields + shared parent login ids → one guardian), class "Year N" → class room, results array → term + subject scores + term report (published), photos from Firebase Storage. AC: dry-run report (counts, unmapped subjects, invalid scores); re-runnable idempotently; reconciliation totals match.
- **E8.3** Everyone receives new credentials (Firebase passwords cannot be exported); printable credential slips per family.

## E9 — Attendance
- **E9.1** 🟡 *API ✅ 2026-10-15 (default present, form teacher today / admin earlier days, school days only, locked after publish); UI pending.* Daily register per class (default all present; mark absent/late/excused with note). AC: one record per student per day; editable same day by form teacher, later by admin only.
- **E9.2** 🟡 *API ✅ 2026-10-15 (class term summary; report cards and publishing use register totals when attendance isn't entered by hand); UI pending.* Attendance summaries per student/class/term; feeds report cards.
- **E9.3 (S)** Absence notification to guardians (E12).

## E10 — Fees & payments
- **E10.1** 🟡 *API ✅ 2026-10-15 (per term, class level or school-wide, optional items by per-student opt-in, copy between terms); UI pending.* Fee items per term and class level (mandatory / optional e.g. bus, lunch).
- **E10.2** 🟡 *API ✅ 2026-10-15 (idempotent, INV-{session}-{seq}, sibling discount None/Percentage/FixedAmount set by admin, 2–3 part instalment plans per term or session); scholarships and cancel pending.* Generate invoices for a term (all students of a level), with discounts/scholarships (sibling discount rule). AC: idempotent per student-term; numbered `INV-{session}-{seq}`.
- **E10.3** 🟡 *API ✅ 2026-10-15 (cash/transfer/POS, RCT-{session}-{seq}, full payment unless instalments enabled, admin reversals as new entries, cancel, adjustments); invoice and receipt PDFs ✅ 2026-10-15.* Record manual payments (cash, transfer, POS) with receipt numbers. AC: cannot exceed balance; cannot pay a cancelled invoice; receipts immutable (reversal = new entry).
- **E10.4** 🟡 *API ✅ 2026-10-15 (student statement with running balance, debtors with overdue-by-plan, CSV export); collections report pending.* Statements, debtors list, collections report by class/term; CSV export.
- **E10.5** 🟡 *API ✅ 2026-10-17 (portal checkout, signed webhook + return-page confirmation recorded once, server-side re-verify of status/NGN/amount, excess and mismatch flagged for review); UI and live keys pending.* Online payment via Paystack: initialise → redirect → **webhook verified by signature** → payment recorded idempotently by gateway reference. AC: amount re-verified server-side via Paystack verify API; replayed webhooks ignored.

## E11 — Admissions pipeline
- **E11.1** Kanban/list of applications: New → Contacted → Assessment scheduled → Offered → Enrolled / Declined, notes, assigned staff.
- **E11.2** Convert to student (pre-fills student + guardian). AC: application links to created student.
- **E11.3 (S)** Email/SMS acknowledgement with reference number.

## E12 — Communication & notifications
- **E12.1** Announcements with audience (public/parents/students/staff), pinning, scheduling.
- **E12.2** Events calendar (public & internal).
- **E12.3** 🟡 *Report-published email with PDF ✅ 2026-10-15 (outbox + retries, Pickup/SMTP/Resend providers, admin delivery status + retry); SMS, in-app, other triggers and opt-out pending.* Notification outbox + providers (email via SMTP/Resend; SMS via Termii/Africa's Talking; in-app). Triggers: result published, absence, invoice issued, payment received, announcement published. AC: retries with backoff; per-recipient opt-out for non-critical messages; delivery status visible to admin.
- **E12.5** **Audience targeting** for announcements & emails: everyone, all parents, all staff, all teachers, a section (e.g. Primary), a class/arm, guardians of specific students, or debtors. AC: preview recipient count before sending; only recipients in the target receive it (tested); staff can only target their own class unless Admin.
- **E12.6** **Newsletters**: compose (rich text + images) → choose audience (parents, staff, public subscribers from the website) → schedule or send → per-recipient tracking. Templates with school branding; unsubscribe link (required for public subscribers). AC: public subscribers confirm by email (double opt-in); unsubscribes honoured immediately.
- **E12.4** Contact-form messages inbox in admin.

## E13 — Dashboards & analytics
- **E13.1** Admin dashboard: enrolment by class/section, today's attendance %, fees billed vs collected (term), applications pipeline, recent activity.
- **E13.2 (S)** Academic analytics: subject averages by class, term-over-term trend per student.

## E14 — Compliance, audit & operations
- **E14.1** 🟡 *API ✅ 2026-10-12 (DB-enforced append-only, ADR-005 §6); viewer UI pending (frontend).* Automatic audit trail of every create/update/delete (who, when, what changed, IP), secrets redacted, append-only. Viewer for admins with filters.
- **E14.2** NDPA 2023: privacy notice, consent capture at admission, data-subject access/export request, retention policy (e.g. withdrawn students' data archived after N years), breach response runbook.
- **E14.3** Backups: daily + PITR; quarterly restore drill documented.
- **E14.4** Security headers, dependency scanning (Dependabot), secret scanning in CI.

## E16 — Online tests & exams (CBT)
Computer-based tests for students, with results flowing into the gradebook.
- **E16.1** Question bank per subject & class level: multiple choice, multi-select, true/false, short answer, essay (manual marking); images in questions; tags by topic & difficulty.
- **E16.2** Create a test/exam: questions (fixed or random from bank), duration, open/close window, attempts, shuffle questions & options, linked to an assessment component (e.g. CAT or Exams) and term.
- **E16.3** *As a student, I take the test in the portal* with a countdown timer; answers autosaved; resumes after a dropped connection within the window; auto-submits at time-out. AC: server is the source of truth for time; answers rejected after deadline.
- **E16.4** Auto-marking for objective questions; teacher marking screen for essays; moderation before release.
- **E16.5** Push marks into the grade sheet (scaled to the component's max) with teacher confirmation. AC: never overwrites a published report.
- **E16.6** Integrity: one active session per attempt, tab-switch/blur events logged, randomised order per student, results hidden until released. (Proctoring by camera is out of scope.)
- **E16.7** Analytics: item difficulty, score distribution per class.

## E15 — Later (phase 3, Could)
Timetable builder; parent ↔ teacher messaging; homework/assignments; library; transport routes & GPS; staff payroll; multi-campus.

---

## Definition of Ready (story)
Clear user, AC written, design (wireframe or component reference) available for UI stories, API contract sketched, dependencies known.

## Definition of Done (story)
1. Code reviewed, follows conventions in `CLAUDE.md`.
2. Server-side validation + authorization (role **and** row-level) with an integration test for the forbidden case.
3. Unit tests for business rules; integration test for each endpoint's happy path.
4. UI: responsive (360 px → desktop), keyboard-accessible, loading/empty/error states, light & dark.
5. Audit trail verified for data changes.
6. OpenAPI client regenerated; no type errors; CI green.
7. Docs updated (this file's status, ADR if a decision was made).
