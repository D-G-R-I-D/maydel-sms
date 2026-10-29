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
| E8 | Legacy data migration (dropped 2026-10-24) | — | — |
| E9 | Attendance | 2 | M |
| E10 | Fees & payments | 2 | M |
| E11 | Admissions pipeline | 2 | S |
| E12 | Communication, notifications & newsletters | 2 | M |
| E13 | Dashboards & analytics | 2 | S |
| E14 | Compliance, audit & operations | 0–2 | M |
| E15 | Timetable, messaging, homework | 3 | C |
| E16 | Online tests & exams (CBT) | 2–3 | S |
| E17 | Homework & communication book | 3 | S |
| E18 | School bus: trip log & driver checklist | 3 | S |
| E19 | Store & inventory (uniforms, books, ID cards) | 3 | S |

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
- **E7.3** ✅ *API 2026-10-19 (posts with drafts/slugs/pictures, events, gallery albums, staff on site, contact inbox).* News, events, gallery, staff profiles managed in admin (no code changes to publish).
- **E7.4** SEO: per-page metadata, Open Graph, `sitemap.xml`, `robots.txt`, JSON-LD `School` schema, canonical URLs.
- **E7.5** Accessibility WCAG 2.2 AA: keyboard nav, focus states, contrast, alt text, reduced-motion. AC: axe has zero serious violations.
- **E7.6** Light/dark theme respecting system preference.
- **E7.7** *(added 2026-10-24)* 🟡 *API ✅ 2026-10-24; UI pending.* **Rich editor** for news, notices, events and newsletters: headings, emphasis, lists, quotes, links, button links, dividers and pictures with captions; cleaned on the server to an allow-list; pictures uploaded and resized.
- **E7.7** Performance: images via `next/image` (AVIF/WebP, responsive), fonts self-hosted via `next/font`. AC: Lighthouse mobile ≥ 90.
- **E7.8** *(added 2026-10-17)* **Main site + portal on one domain.** Public website on `www.<domain>`, the portal (staff console + parents/students) on `portal.<domain>`. One Next.js app, routing by host in middleware, so there's one deploy, shared design and one BFF. Auth cookies are scoped to the portal host only, and the public site never sees them. AC: `www` serves only public pages; `portal` serves sign-in and the app; canonical URLs and sitemaps per host; a "Portal" button on the website.
- **E7.9** ✅ *API 2026-10-19 (highlights endpoint: carousel slides + ticker).* *(added 2026-10-17)* **Homepage highlights.**
  - An auto-advancing carousel of featured news and gallery photos chosen in admin (right-to-left slide, about 6 s).
  - Pauses on hover, focus or touch; swipe and arrow keys; visible dots; **no autoplay when the user prefers reduced motion**.
  - Plus a slim scrolling news ticker for short notices.
  - AC: keyboard-operable; doesn't shift layout; images optimised.
- **E7.10** ✅ *API 2026-10-19 (notices with window, pop-up flag, version for once-per-visitor).* *(added 2026-10-17)* **Public notices envelope.**
  - An envelope icon in the website header with a badge counting **unread** notices. Unread is stored per browser, so no account is needed.
  - Notices marked *featured* pop up once as a dismissible message, with an optional call-to-action link (e.g. "Admission forms now on sale → Buy form").
  - Admin sets title, body, link, start/end dates and audience *Public* (E12.1).
  - AC: never blocks the page; Esc / ✕ closes it; it's shown at most once per visitor until edited.

## E8 — Legacy data migration
> **Dropped 2026-10-24.** The school starts fresh on the new server; the old Firebase portal's data is not moved.

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
- **E10.6** ✅ *API 2026-10-17.* **New vs returning students.** A fee item applies to *All*, *New intake only* (e.g. acceptance fee, uniform pack, books, development levy) or *Returning only*. Optional items are either **off by default** (only ticked students pay, e.g. bus) or **on by default** (everyone pays unless unticked, e.g. books, lunch); the admin or bursar picks per item. A student is "new" in the term they were admitted. Invoices show a "New intake" tag, and lists can filter by it. AC: generation bills each student only the items for their group; tested for both groups.
- **E10.7** ✅ *API 2026-10-17.* **Publish invoices and notify parents.**
  - Generated invoices start as *Draft*: staff-only, editable, and they can be regenerated.
  - **Publish** (per class level or all) makes them visible in the portal and queues an email to each guardian with the invoice PDF, a portal link, and "quote admission number MSS/…".
  - AC: drafts are never visible to parents (tested); publishing again doesn't re-email.
- **E10.8** ✅ *API 2026-10-17.* **Proof of payment upload.**
  - A parent who paid by bank transfer or at the bank uploads the teller or screenshot (image or PDF, private, re-encoded) against an invoice in the portal, with amount, date and bank reference. This replaces "reply to the email with the receipt".
  - The bursar works a *To verify* queue: **Confirm** records the payment and issues an RCT receipt linked to the proof; **Reject** sends a reason to the parent.
  - AC: the parent sees the status; nothing is recorded until confirmed; the proof stays attached to the receipt.
- **E10.9** *(added 2026-10-17)* **Korapay as an alternative gateway.**
  - Same `IPaymentGateway` abstraction as Paystack. Whichever provider has keys configured is offered; if both do, the admin picks the primary in Settings → Fees.
  - Webhooks are verified per provider, and the same re-verify / record-once / NeedsReview rules apply.
  - AC: switching provider needs no code change; references can't collide between providers.

## E11 — Admissions pipeline
- **E11.1** Kanban/list of applications: New → Contacted → Assessment scheduled → Offered → Enrolled / Declined, notes, assigned staff.
- **E11.2** ✅ *API 2026-10-17 (via E11.8 enrol).* Convert to student (pre-fills student + guardian). AC: application links to created student.
- **E11.3 (S)** Email/SMS acknowledgement with reference number.
- **E11.4** ✅ *API 2026-10-17 (applicant account at purchase, online or school form code, exam window opens on payment).* **Buy the admission form.**
  - Online through the gateway (E10.5/E10.9), or at the school (the bursar issues a form code).
  - Gives an application reference and an email with the receipt and a link to fill the form.
  - AC: the form fee is configurable per session; one purchase = one application.
- **E11.5** ✅ *API 2026-10-17 (draft/submit, private documents, blank and filled PDFs, admin corrections after submission).* *(added 2026-10-17)* **Online application form** in the portal.
  - Child and guardian details, previous school, documents (private uploads).
  - Save as draft, submit, and **download/print as PDF** (blank before, completed after).
  - AC: required fields validated; it can't be edited after submission except by admin.
- **E11.6** ✅ *API 2026-10-17 (sittings with seats, booking in the window, reminders, Missed sweep, absent, reopen, scores; CBT delivery waits for E16).* *(added 2026-10-17)* **Entrance exam booking.**
  - After buying the form, the applicant books a slot inside a window (admin-configurable, default **3–5 days** after purchase), from times the school opens.
  - Reminder emails go out before the slot.
  - When the window closes unbooked or unattended, the application is marked *Missed* and the school can reopen it.
  - The exam runs as CBT (E16) or on paper, with scores entered by staff.
  - AC: server time is the source of truth; slot capacity is respected.
- **E11.7** ✅ *API 2026-10-17 (cut-off, took/passed/failed lists, CSV, first-stage emails once each).* *(added 2026-10-17)* **Results and cut-off.**
  - Admin sets a **cut-off mark**, then sees lists of *took the exam*, *passed* and *failed*, with scores, plus CSV export.
  - Passed applicants get a "you passed the first stage; the school will contact you about the next step" email.
  - AC: changing the cut-off recomputes the lists before anything is sent.
- **E11.8** ✅ *API 2026-10-17 (draft ticks, publish with reserved admission numbers, letter PDF with signature, regret emails, accept/decline, enrol into student + guardians + parent login; NDPR purge after 6 months, idle applicant accounts off after 12).* *(added 2026-10-17)* **Offers and letters.**
  - From the passed list, admin ticks who gets admission, then **publishes** the decisions.
  - Offered applicants: an email with an **admission letter PDF** carrying the admission number and the **principal's uploaded signature** (E2.6), plus a short welcome guide: what's next, creating the parent account, paying fees online, seeing results.
  - Not offered: a kind "better luck next time" email with no letter.
  - Accepting the offer → **Enrol** (E11.2) creates the student, guardians and parent login, and bills new-intake fees (E10.6).
  - AC: decisions are audited; letters are regenerated identically on reprint; no email is sent before publish.
- **E11.9** *(added 2026-10-17)* **Stage emails** through the E12 outbox: form bought, form submitted, exam booked / reminder, result, offer or regret, enrolled. Sent from `admissions@<domain>`.

## E12 — Communication & notifications
- **E12.1** Announcements with audience (public/parents/students/staff), pinning, scheduling.
- **E12.2** Events calendar (public & internal).
- **E12.3** 🟡 *Report-published email with PDF ✅ 2026-10-15 (outbox + retries, Pickup/SMTP/Resend providers, admin delivery status + retry); SMS, in-app, other triggers and opt-out pending.* Notification outbox + providers (email via SMTP/Resend; SMS via Termii/Africa's Talking; in-app). Triggers: result published, absence, invoice issued, payment received, announcement published. AC: retries with backoff; per-recipient opt-out for non-critical messages; delivery status visible to admin.
- **E12.5** **Audience targeting** for announcements & emails: everyone, all parents, all staff, all teachers, a section (e.g. Primary), a class/arm, guardians of specific students, or debtors. AC: preview recipient count before sending; only recipients in the target receive it (tested); staff can only target their own class unless Admin.
- **E12.6** 🟡 *API ✅ 2026-10-24 (editor content, audiences by group/section/class, test, send or schedule, branded email, unsubscribe + List-Unsubscribe, double opt-in, daily limit, website archive); UI pending.* **Newsletters**: compose (rich text + images) → choose audience (parents, staff, public subscribers from the website) → schedule or send → per-recipient tracking. Templates with school branding; unsubscribe link (required for public subscribers). AC: public subscribers confirm by email (double opt-in); unsubscribes honoured immediately.
- **E12.4** Contact-form messages inbox in admin.
- **E12.8** *(added 2026-10-24)* **Fee notifications.** Every recorded or confirmed payment creates an in-app notice for the family and emails the receipt (from `accounts@`). Overdue invoices trigger a reminder (in-app + email) when they become overdue, then at most weekly until paid; the admin can switch reminders off or change the interval. AC: one notice per payment (no duplicates on retries); reminders stop the day the balance is cleared; only that family's guardians are notified.
- **E12.7** *(added 2026-10-17)* **Sender addresses on the school domain, per purpose:** `admissions@`, `accounts@` (invoices, receipts), `results@` (report cards), and `no-reply@` for the rest. Replies go to a monitored inbox. SPF/DKIM/DMARC are set up on the domain so mail isn't flagged as spam.

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
- **E16.1** ✅ *API 2026-10-18 (all six kinds incl. fill-in-the-gap, keys, marking guides, topic/difficulty tags, pictures, shared per subject+level with teacher row-level access; archive not delete).* Question bank per subject & class level: multiple choice, multi-select, true/false, fill-in-the-gap, short answer, essay (manual marking); images in questions; tags by topic & difficulty.
- **E16.2a** ✅ *API 2026-10-19 (sections, totals, scaling preview, scaled push to the grade sheet after confirmation).* *(added 2026-10-17)* **Mixed sections and automatic mapping.**
  - A test/exam is built from sections, e.g. 40 objective + 10 true/false + 10 fill-in-the-gap + 3 theory, each with its own marks per question and its own instructions.
  - Objective, true/false and fill-in-the-gap are auto-marked; theory is teacher-marked against the marking guide.
  - The total is **scaled automatically** to the assessment component it feeds (e.g. a 100-mark exam becomes /40 for "Exams" or /30 for "CAT") and goes into the grade sheet after the teacher confirms.
  - AC: section totals and scaling are shown before publishing; rounding follows the report rules.
- **E16.2** ✅ *API 2026-10-18 (fixed or random-from-bank sections, window, attempts, shuffle, component + term, readiness checks, publish/unpublish).* Create a test/exam: questions (fixed or random from bank), duration, open/close window, attempts, shuffle questions & options, linked to an assessment component (e.g. CAT or Exams) and term.
- **E16.3** ✅ *API 2026-10-18 (per-student papers, server deadline, autosave, resume/takeover, auto-submit, objective marking on hand-in).* *As a student, I take the test in the portal* with a countdown timer; answers autosaved; resumes after a dropped connection within the window; auto-submits at time-out. AC: server is the source of truth for time; answers rejected after deadline.
- **E16.4** ✅ *API 2026-10-19 (auto-marking at hand-in, teacher marking by student or by question with overrides and comments, release after close when all marked).* Auto-marking for objective questions; teacher marking screen for essays; moderation before release.
- **E16.5** ✅ *API 2026-10-19 (preview raw → scaled per student, confirm, writes only that component, skips published reports).* Push marks into the grade sheet (scaled to the component's max) with teacher confirmation. AC: never overwrites a published report.
- **E16.6** ✅ *API 2026-10-19 (one live session token, blur/focus/takeover log, per-student order, results hidden until release).* Integrity: one active session per attempt, tab-switch/blur events logged, randomised order per student, results hidden until released. (Proctoring by camera is out of scope.)
- **E16.7** ✅ *API 2026-10-19 (mean/median/high/low, 10-band distribution, per-arm means, per-question facility and option picks).* Analytics: item difficulty, score distribution per class.
- **E16.8** ✅ *API 2026-10-19 (Claude claude-opus-5-5 via the official SDK, PDF/photo material, schema-checked drafts with keys and guides, 5 regenerations, edit and save to bank; needs `Ai:ApiKey`).* *(added 2026-10-17)* **AI-assisted question writing for teachers.**
  - The teacher picks subject, class, term and assessment type (test / CAT / exam), then uploads the scheme of work, notes (PDF) or a photo of notes. Files are stored privately.
  - They choose question kinds (objective, multi-select, true/false, short answer, theory), how many, and the difficulty mix.
  - The AI (Claude, server-side) generates questions **only from the uploaded material and the chosen subject/class scope**, each with an answer key and, for theory, a marking guide.
  - The teacher can **regenerate up to 5 times** per request, then accept any version, edit questions and options freely, or write their own.
  - Saved questions go to the question bank (E16.1) and are published as a test/CAT/exam with an availability window (E16.2/E16.3).
  - AC: no student data is ever sent to the AI; output is always reviewable and editable before students see it.
- **E16.9** ✅ *API 2026-10-19 (monthly allowance per teacher with default + overrides, school cap, failures free, usage dashboard).* *(added 2026-10-17)* **Per-teacher AI quota.**
  - Each teacher has their own monthly allowance (generations / tokens), set by admin, with a default that's equal for all. One teacher using theirs never reduces anyone else's.
  - There's a school-wide cap and usage dashboard for admin.
  - AC: a request over quota is refused with a clear message, and the remaining allowance is shown before generating.

## E15 — Later (phase 3, Could)
Timetable builder; library; transport routes & GPS; staff payroll; multi-campus. (Homework and parent ↔ teacher messaging moved to E17; the bus trip log to E18.)

## E17 — Homework & communication book *(added 2026-10-24)*
- **E17.1** 🟡 *API ✅ 2026-10-25 (set by subject or form teacher with editor instructions and files, hand-ins with text and files, late flag, roster, seen/returned with feedback, notifications); UI pending.* **Homework.** A teacher posts homework to a class (and subject they teach): title, instructions, attachments, due date. Students see it on their dashboard and hand in text and/or files; parents see their child's homework and whether it's done. The teacher sees who has handed in, marks it seen or gives a short comment. AC: teachers only post to their own classes; students and parents only see their own class's homework (tested).
- **E17.2** 🟡 *API ✅ 2026-10-25 (form teacher/admin ↔ guardians/student, files, 15-minute corrections, per-user unread, notifications, evening email digest ✅ 2026-10-29); UI built.* **Communication book.** One running notebook per student between the class teacher (and admin) and that student's guardians; the student can read and write too. Entries are short notes with an optional attachment; each side sees when the other has read. New entries create an in-app notice (and an email digest at most daily). AC: only the class teacher, admins, the student and their guardians can open a book (tested); entries can't be edited after 15 minutes, only followed up.

## E18 — School bus *(added 2026-10-24)*
- **E18.1** 🟡 *API ✅ 2026-10-25; UI pending.* **Buses and drivers.** Admin registers buses (name, plate, capacity) and assigns a driver (a staff member with the Driver role, signs in on their phone).
- **E18.2** 🟡 *API ✅ 2026-10-25 (tap Left/Back, 2-minute undo, board, CSV, audited corrections); UI pending.* **Trip log.** Each school day has runs: morning pickup Batch A, morning pickup Batch B, afternoon drop-off Batch A, afternoon drop-off Batch B (the admin can rename or add runs). The driver taps *Left* and *Back* for each run; times are recorded in WAT, with an optional note. Admin sees today's runs live and a report by date range (CSV). AC: one tap per event, undo within 2 minutes; admin corrections need a note and are audited.
- **E18.3** 🟡 *API ✅ 2026-10-25 (editable questions, flags notify admins); UI pending.* **Daily driver checklist.** Before the first run the driver answers a short checklist (fuel, tyres, lights, brakes, cleanliness, odometer, any incident) as yes/no or a number, with a notes box. The admin edits the questions. A *No* or an incident flags the day for the admin. AC: takes under a minute on a phone; history per bus.

## E19 — Store & inventory *(added 2026-10-24)*
- **E19.1** 🟡 *API ✅ 2026-10-25 (items with options, sections, starter uniform list unpriced); UI pending.* **Catalogue.** Items by category (Uniform, Sportswear, Books, ID cards, Other) with sizes or variants, price, and who it's for (Nursery, Primary, Secondary or all). The admin adds, edits, re-prices or retires items. Starter list (editable): black blazer, white shirt, tie, black skirt, black trousers (Secondary), black shorts (Primary), sportswear per house colour (houses are set by the admin). Nursery items are added by the admin.
- **E19.2** 🟡 *API ✅ 2026-10-25 (restock, adjust with reason, movement log, low stock, oversell guard); UI pending.* **Stock.** Quantity per variant, restock entries (supplier, quantity, cost), low-stock alert level per item, stock count adjustments with a reason.
- **E19.3** 🟡 *API ✅ 2026-10-25 (office sale cash/transfer/POS or to fees; receipt PDF and student ID cards ✅ 2026-10-26).* **Selling or issuing.** Sell to a student either as a cash/transfer sale (receipt PDF) or added to the student's fee account (an invoice line, E10). ID cards can be issued per student and reprinted. AC: stock can't go below zero; every movement is audited; parents see store purchases on their portal statement.
- **E19.4** 🟡 *API ✅ 2026-10-25 (section-filtered shop, hold + release, ready notification, pay at office or fees); online payment waits for gateway keys; UI pending.* **Parent shop (portal).** Parents buy for their child online: they see only the items for the child's section, pick sizes, and pay online (Paystack, E10.5), add it to the fees account, or choose pay at the office. Orders go Placed → Paid → Ready for collection → Collected (or Cancelled); store staff get a picking list, and the family gets a notification when it's ready. AC: stock is reserved when the order is placed and released if it's cancelled or unpaid after 48 hours; a parent only orders for their own children (tested); online payments are verified with Paystack before the order counts as paid.

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
