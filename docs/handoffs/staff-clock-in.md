# Handoff → Frontend: staff clock-in / clock-out (rotating kiosk QR)

> Backend status as of 2026-10-24. Contract: `contracts/openapi.json` (run `npm run api:types`).

## How it works
1. The admin opens **Settings → Staff clock-in** and issues a **kiosk link**: `POST /api/clock/kiosk-link` returns `{ token }` **once**. Only its hash is stored, so issuing a new link kills the old one.
2. A screen at the school entrance (a tablet or an old phone) opens the kiosk page `/clock/kiosk?token=…`. The page needs no sign-in.
   - The page polls `GET /api/public/clock/kiosk?token=…` and shows `url` as a QR code.
   - The code changes every 30 s (`expiresAt`), so a photo of it sent over WhatsApp stops working quickly. The previous code is still accepted, to allow for slow phones.
3. A staff member scans the QR with their phone camera. It opens `{portal}/clock?c=<code>`. They sign in if needed (keep `c` through the login redirect).
   - The page calls `POST /api/clock { code, latitude?, longitude? }`.
   - The **first scan of the day clocks in**; **the second clocks out** (at least 1 minute later).
4. The admin sees today's board, a report for any date range (plus CSV), and can correct a day. A correction needs a note and is audited.

**Location (optional):** when `requireLocation` is on, the clock page must send the browser's geolocation. The scan is refused beyond `radiusMeters` of the school's lat/lon. Use "Use this device's location" in settings to fill lat/lon from the kiosk.

**Late:** a clock-in after `startsAt + graceMinutes` (WAT) is marked late. Weekends are refused.

## Endpoints
| Method & path | Who | Body / query | Response |
|---|---|---|---|
| `POST /api/clock` | staff (teacher, bursar, admin with a staff record) | `{ code, latitude?, longitude? }` | `ClockResult { clockedIn, day: StaffDay }` (`clockedIn=false` means it was a clock-out) |
| `GET /api/clock/me?from&to` | staff | default: last 30 days | `StaffDay[]`, newest first |
| `GET /api/public/clock/kiosk?token` | anonymous (rate-limited) | | `{ code, url, expiresAt, school }`; 401 `clock.kiosk` if the token is revoked |
| `GET /api/clock/settings` | admin | | `{ startsAt "07:30:00", endsAt, graceMinutes, requireLocation, latitude, longitude, radiusMeters, kioskLinkIssued }` |
| `PUT /api/clock/settings` | admin | same fields without `kioskLinkIssued` | the settings |
| `POST /api/clock/kiosk-link` | admin | | `{ token }`: build `{origin}/clock/kiosk?token=` and show it once, with a copy button |
| `GET /api/clock/board?date` | admin | default today | `{ date, schoolDay, present, late, absent, staff: StaffDay[] }`: every active staff member, with absent ones as `state: "Absent"` |
| `GET /api/clock/report?from&to` | admin | up to a year | `{ from, to, schoolDays, staff: [{ staffId, name, jobTitle, schoolDays, daysPresent, daysLate, daysAbsent, averageArrival, hoursWorked }] }` |
| `GET /api/clock/report.csv?from&to` | admin | | CSV download |
| `PUT /api/clock/days/{staffId}/{date}` | admin | `{ clockInAt?, clockOutAt?, note }` | `StaffDay` (`method: Manual`) |

`StaffDay = { staffId, name, jobTitle, date, clockInAt, clockOutAt, isLate, state: In|Late|Out|Absent, hoursWorked, inMethod: Qr|Manual, outMethod, note }`

## Error codes
| Code | Status | Show |
|---|---|---|
| `clock.code` | 422 | "Code expired, scan again" (offer a rescan) |
| `clock.not_staff` | 403 | the account has no staff record |
| `clock.weekend` | 422 | |
| `clock.too_far` | 422 | the message includes the distance |
| `latitude` (field) | 400 | location permission is needed |
| `clock.too_soon` / `clock.done` | 422 | already clocked in, or already done for today |
| `clock.twice` | 409 | a double-submit; refetch `/me` |
| `clock.kiosk` | 401 | kiosk page: "This link was replaced; ask the admin" |
| `clock.hours` / `clock.grace` / `clock.radius` / `clock.location` | 400 | settings form fields |
| `clock.note` / `clock.order` / `clock.day` | 400 | correction form fields |

## UI to build
- **Kiosk page** (`/clock/kiosk`): full screen, large QR, school name, a live WAT clock and a countdown ring to `expiresAt`. Refetch about 2 s before expiry, keep it light and dark, and avoid screen burn.
- **Clock page** (`/clock?c=`): one big result card ("Clocked in 07:42, on time" / "Clocked out 16:05 — 8.4 h"), plus a status chip on the staff dashboard and "My attendance" history.
- **Admin, Staff attendance:**
  - Today board (present/late/absent counts, a table, auto-refresh);
  - Report with a date range, CSV export, and per-staff drill-down;
  - Correct-day dialog (in/out time pickers in WAT, note required);
  - Settings (hours, grace, location toggle with map pin and radius, kiosk link issue/replace).
- Show all times in **WAT**.

## Demo
- Teachers and bursars from the seed have staff records. Issue a kiosk link as admin, open it in one tab, and copy the `url` into a teacher session.
