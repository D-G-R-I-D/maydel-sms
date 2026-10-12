# 05 — Design System & UX

We **evolve** the existing brand rather than copy the legacy pages. The legacy site has a strong editorial look (navy + gold, Fraunces serif headings), but it suffers from inline styles, a single 900-line page toggled by JS (bad for SEO and deep links), emoji used as icons, two different visual languages between site and portal, and placeholder staff cards ("Add photo").

## 1. Principles
1. **Calm & trustworthy** — parents are trusting us with their children: generous whitespace, restrained motion, no gimmicks.
2. **Phone-first** — most parents use mid-range Android phones on mobile data. Design at 360 px first.
3. **One system** — public site, staff console and portal share the same tokens and components; they differ in density, not style.
4. **Accessible by default** — WCAG 2.2 AA, visible focus, 44 px touch targets, `prefers-reduced-motion` respected.
5. **Data first in the console** — tables, filters and keyboard flow beat decoration for staff doing repetitive work.

## 2. Tokens

### Colour (light / dark)
| Token | Light | Dark | Use |
|---|---|---|---|
| `--brand` (navy) | `#234a6d` | `#6d9dc5` | Primary actions, headings, links |
| `--brand-strong` | `#183754` | `#a7c5e0` | Hover/pressed, hero backgrounds (light) |
| `--accent` (gold) | `#d9b16a` | `#e0b973` | Highlights, eyebrow text on navy, key CTA on dark |
| `--accent-strong` | `#b8924a` | `#f0d7a3` | Gold text on light backgrounds (AA-compliant) |
| `--crimson` | `#b42332` | `#e4646f` | Crest accent; destructive actions |
| `--bg` | `#f7f9fb` | `#0d1a26` | App background |
| `--surface` | `#ffffff` | `#142536` | Cards, panels |
| `--surface-2` | `#ebf1f7` | `#1a2d40` | Table headers, subtle fills |
| `--border` | `#d6e1ec` | `#2a3d51` | Dividers, inputs |
| `--text` | `#1f2c38` | `#e9eef4` | Body |
| `--muted` | `#586d7d` | `#9aadc0` | Secondary text |
| `--success` / `--warning` / `--danger` | `#2f8f57` / `#b7791f` / `#c0392b` | lighter equivalents | Status |

Rules: gold text only on navy or as `--accent-strong` on light (contrast ≥ 4.5:1). Never convey status by colour alone (icon + label).

### Typography
- **Display:** Fraunces (variable, optical size) — page titles, section headings, hero. Italic for the signature emphasis (*"strong, joyful start."*).
- **Body/UI:** Inter — everything else; tabular numerals (`font-variant-numeric: tabular-nums`) in tables, scores and money.
- Scale (rem): 0.75 · 0.875 · 1 · 1.125 · 1.25 · 1.5 · 2 · 2.5 · 3.5 (fluid with `clamp()` for display sizes).
- Self-hosted via `next/font` (no render-blocking Google Fonts request).

### Space, radius, elevation, motion
- 4 px base grid; section rhythm 64/96/128 px desktop, 48/64 mobile.
- Radius: 8 (inputs, chips) · 14 (cards) · 22 (feature panels) · full (pills, avatars).
- Shadows: navy-tinted, low (`0 2px 8px rgb(35 74 109 / .08)`); dark mode uses borders over shadows.
- Motion: 150–250 ms ease-out; marquee/counters/parallax disabled under reduced motion.

### Icons & imagery
- **Lucide** icon set (consistent stroke) replaces emoji.
- Real school photography, `next/image` responsive AVIF/WebP, consistent 4:3 / 16:9 crops; staff without photos get monogram avatars (no "Add photo" placeholders in production).

## 3. Components (built once, used everywhere)
Button (primary, secondary, ghost, destructive, link; sizes; loading) · IconButton · Input / Textarea / Select / Combobox / DatePicker · Checkbox / Radio / Switch · Form field (label, hint, error) · Card / StatCard · Badge / StatusPill · Table (sortable, sticky header, empty/loading/error rows, responsive → stacked cards on mobile) · Pagination · Tabs · Dialog / Drawer / ConfirmDialog · Toast · Tooltip · Avatar · Breadcrumbs · PageHeader (title, description, actions) · EmptyState · Skeleton · FileDrop (with preview) · ScoreGrid (spreadsheet-like editor for E5) · ReportCard (screen + print) · ThemeToggle.

Built on **Radix UI primitives** (focus management, ARIA) styled with Tailwind using the tokens above.

## 4. Information architecture

### Public site (each page is a real URL — fixes legacy single-page tab switching)
```
/                 Home — hero, why us (pillars), programmes, stats, testimonials, news, events, FAQ, CTA
/about            Story, founder, vision/mission/values, accreditation (WAEC/NECO)
/academics        Programmes by section (Creche → SSS), curriculum, clubs
/admissions       Process (4 steps), apply form, requirements, calendar, fees guidance, FAQ
/parents          Hours, transport, security/CCTV, uniform, meals, policies, FAQ
/staff            Leadership + teachers (from CMS)
/gallery          Filterable masonry + lightbox (from CMS)
/news, /news/[slug], /events
/contact          Form, map, phone/WhatsApp, office hours
/login            Unified sign-in (password + Google)
/privacy          NDPA privacy notice
```
Header: logo · Academics · Admissions · Parents · About · News · Contact · **Portal** button. Sticky, condenses on scroll; mobile drawer.

### Staff console (`/admin`)
Sidebar groups: **Overview** (Dashboard) · **People** (Students, Guardians, Staff) · **Academics** (Classes, Subjects, Sessions & Terms, Score entry, Report cards) · **Attendance** · **Finance** (Fee items, Invoices, Payments, Debtors) · **Admissions** · **Website** (News, Events, Gallery, Messages) · **Settings** (School profile, Assessment, Users) · **Audit log**. Items filtered by role (a teacher sees Dashboard, My classes, Score entry, Attendance, Report comments).

### Portal (`/portal`)
Bottom tab bar on mobile: **Home · Results · Fees · Attendance · More**. Child switcher in the header for parents with several children.

## 5. Key screen notes
- **Score entry:** select class → subject → term (remembered). Grid: rows = students, columns = components + Total + Grade + Comment. Arrow/Enter navigation, invalid cells red with max hint, sticky first column, "Saved ✓" indicator. Locked rows show a padlock with "Report published".
- **Report card:** mirrors the school's paper template (crest, school name & motto, student photo, biodata strip, subject table with components, grading key, attendance, affective traits, comments, signature, next-term date). Separate print stylesheet; PDF generated server-side for identical output.
- **Parent home:** one glance answers "How is my child doing? Do I owe anything? Is anything happening?" — three cards, then notices.
- **Empty states** always explain the next action ("No scores yet — subject teachers enter scores under Academics → Score entry").
