# 00 — Vision & Scope

> Maydel Spring Schools (Abuja) — Creche → Senior Secondary, ~500 students, ~47 staff, approved WAEC & NECO centre.

## 1. Why we are rebuilding

The current system (`/website`) is a static marketing page plus a Firebase "portal". An audit of it found:

| # | Problem in legacy system | Consequence | How the rebuild fixes it |
|---|---|---|---|
| L1 | Portal page loads `parent-portal-minimal.js`, a stub that only toggles screens | **Portal is currently non-functional** | Real API + portals |
| L2 | Firebase API keys + admin ID (`"admin"`) hard-coded in client JS; logins are fake emails `id@maydelspringschools.local` | Security depends entirely on Firestore rules nobody can see | Server-side auth, RBAC enforced in API, no secrets in client |
| L3 | All results stored as a JSON array inside the student document | No class rankings, no analytics, no concurrent edits, full-document overwrite | Normalised scores table; positions/averages computed |
| L4 | Siblings linked by sharing a "Parent Login Student ID" string | Fragile, one password per family hack | Proper Guardian ↔ Student many-to-many |
| L5 | Subjects & assessment weights hard-coded in JS; maxima sum to **105** not 100 | Wrong totals; any change needs a developer | School-configurable components (validated = 100) and grade bands |
| L6 | Results visible to parents the moment they are typed | Half-entered results leak | Draft → Published workflow |
| L7 | Fees = two free-text numbers on the student | No invoices, receipts, history or debtors list | Fee structure, invoices, payments, receipts |
| L8 | Attendance = three numbers typed per term | No daily register, no absence alerts | Daily register; term totals derived |
| L9 | Website content (news, staff, gallery) hard-coded in HTML; forms go to Formspree | Every update needs code; leads lost in an inbox | CMS-lite managed from admin; enquiries tracked in an admissions pipeline |
| L10 | Duplicate 1,700-line JS files, 2,000-line CSS, inline styles, `deepseek_*` scratch files | Unmaintainable | Typed, modular, tested codebase |

**We keep:** the brand (navy `#234a6d`, gold `#d9b16a`, Fraunces + Inter), the editorial tone of the public site, the content, and the school's report-card layout. **We enhance** everything else.

## 2. Goals (12 months)

1. **Replace the legacy site and portal** with zero loss of student/result data (migration from Firestore).
2. **Report cards in hours, not days**: teachers enter scores per subject; positions, averages, attendance and grades are computed; admin publishes per class.
3. **Fee visibility**: every family can see invoices, payments and balances; bursar sees debtors in one click.
4. **Parents are informed**: published results, absences, announcements and events reach them on their phone.
5. **Secure by default**: least-privilege roles, audited changes, NDPA-2023-aware handling of children's data.

### Success metrics
| Metric | Target |
|---|---|
| Time from end-of-term exams to all report cards published | ≤ 5 working days |
| Parents with an active portal login | ≥ 80 % by end of first term |
| Fee balances visible online | 100 % of enrolled students |
| Lighthouse (public site, mobile) | Performance ≥ 90, Accessibility ≥ 95, SEO ≥ 95 |
| P95 API latency | < 300 ms |
| Unplanned downtime during term | < 4 h / term |

## 3. Personas

| Persona | Needs | Primary surface |
|---|---|---|
| **Proprietress / Admin** | Oversight, publish results, manage staff, see fees & enrolment at a glance | Admin console |
| **Class (form) teacher** | Register attendance, enter scores, write report comments for *their* class | Staff console (mobile-friendly) |
| **Subject teacher** | Enter scores for subjects they teach across classes | Staff console |
| **Bursar** | Fee structure, invoices, record payments, receipts, debtors | Staff console – Finance |
| **Parent / guardian** | One login for all children; results, fees, attendance, notices | Parent portal (PWA, phone-first) |
| **Student** | Own results, timetable, notices | Student portal |
| **Prospective parent** | Learn about the school, apply, book a visit | Public website |

## 4. Scope

**In scope (v1, phases 0–2):** public website + CMS-lite, identity & access (incl. Google sign-in), school setup, admissions pipeline, student information, staff records, attendance, assessment & report cards, fees & payments (manual + online gateway), announcements & events, parent/student portal, admin dashboard, audit, migration.

**Later (phase 3+):** timetable, SMS/WhatsApp notifications at scale, parent–teacher messaging, homework/LMS-lite, library, transport tracking, payroll/HR, multi-campus.

**Out of scope:** full LMS, accounting ledger (we export to the school's accountant), biometric devices.

## 5. Constraints & assumptions

- Nigerian context: NGN currency, Paystack/Flutterwave, intermittent connectivity → phone-first, lightweight pages, PWA.
- Small IT capacity at the school → managed hosting, zero-ops where possible, admin features must be self-explanatory.
- Children's personal data → Nigeria Data Protection Act 2023 (NDPA): consent, minimisation, access control, breach procedure.
- Budget-conscious hosting (single region, single API instance is sufficient for ~1,000 users; design must still scale horizontally).
