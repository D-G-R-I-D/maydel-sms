# Handoff → Frontend: school bus (E18)

> Backend status as of 2026-10-25. Contract: `contracts/openapi.json` (run `npm run api:types`).

## Roles
- **New role `Driver`.** Create drivers as staff with role `Driver` (Settings → Staff; the role list now includes it).
  - Drivers see **no student data**: `/api/students` etc. return 403.
  - They **clock in and out** like other staff. `/api/clock` now uses a new Workforce policy covering admin, teacher, bursar and driver.
- **Admins** do everything: they can log a trip or a checklist for an absent driver from the driver endpoints too.

## How it works
1. The admin adds **buses** (name, plate, capacity, driver).
2. **Runs.** Four runs exist by default and can be renamed, reordered, added to, or retired (never deleted):
   - Morning pickup – Batch A, Morning pickup – Batch B;
   - Afternoon drop-off – Batch A, Afternoon drop-off – Batch B.
3. **The driver's day:** open the driver page on the phone.
   - Do the **daily checklist** once per bus per day (submitting again replaces it).
   - For each run, tap **Left** when the bus sets off and **Back** when it returns. Times are recorded in WAT.
   - A mis-tap can be **undone within 2 minutes**. After that, the admin corrects it, with a reason that is kept.
4. **Checklist flags.** A "wrong" yes/no answer (e.g. "Tyres look OK?" → No) or anything written in "Any incident?" **flags** the day. Admins get a `BusCheckFlagged` notification (link `/admin/transport`).
5. **Admin views:** today's board (every bus × run, plus the checklist), a date-range report with CSV, and checklist history (optionally flagged only).

## Admin endpoints (`/api/transport`, admin)
| Method & path | Body | Response |
|---|---|---|
| `GET /buses` · `POST /buses` · `PUT /buses/{id}` | `{ name, plateNumber?, capacity, driverStaffId?, isActive }` | `BusDto { id, name, plateNumber, capacity, driverStaffId, driverName, isActive }` |
| `GET /runs` · `PUT /runs` | `PUT`: the whole list `[{ id?, name, kind: Pickup \| DropOff, order, isActive }]` | `RunDto[]` |
| `GET /questions` · `PUT /questions` | `PUT`: the whole list `[{ id?, text, kind: YesNo \| Number \| Text, order, isActive, expectedYes, flagIfAnswered }]` | `CheckQuestionDto[]` |
| `GET /board?date` | | `BusDayBoard { date, buses: [{ bus, trips: TripDto[] (every active run), check: CheckDto? }] }` |
| `GET /trips?from&to&busId` · `GET /trips.csv?…` | | `TripDto[]`, or the CSV download |
| `PUT /trips/{busId}/{runId}/{date}` | `{ leftAt?, backAt?, note }` (note required) | `TripDto` |
| `GET /checks?from&to&flaggedOnly&busId` | | `CheckDto[]`, newest first (default: last 30 days) |

## Driver endpoints (`/api/driver`, driver or admin)
| Method & path | Body | Response / errors |
|---|---|---|
| `GET /today` | | `DriverToday { date, buses: BusDay[], questions: CheckQuestionDto[] }`, with only *your* buses |
| `POST /trips` | `{ busId, runId, event: "Left" \| "Back", note? }` | `TripDto`; 422 `trip.already_left` / `trip.not_left` / `trip.already_back`; 404 `bus.not_yours` |
| `POST /trips/undo` | `{ busId, runId }` | `TripDto`; 422 `trip.undo_window` / `trip.nothing` |
| `POST /checks` | `{ busId, answers: [{ questionId, yes?, number?, text? }], notes? }` | `CheckDto`; 400 `answers` (every yes/no and number question must be answered) |

- `TripDto = { busId, bus, runId, run, kind, date, leftAt, backAt, minutes, note, correction, canUndo }`.
- `CheckDto = { id, busId, bus, date, driverName, submittedAt, isFlagged, notes, answers: [{ questionId, question, kind, yes, number, text, flagged }] }`.

## UI to build
- **Driver page (`/driver`, mobile-first, big buttons):**
  - for each bus: today's checklist (a card that turns green once done);
  - for each run, a row with a big **Left** / **Back** button, the times, and **Undo** while `canUndo`;
  - the date and a WAT clock at the top;
  - after login, a driver lands here.
- **Admin, Transport:**
  - **Today:** a buses × runs grid with times, durations, checklist status and a red flag.
  - **Reports:** a date range, bus filter and CSV export.
  - **Checklists:** history with a flagged filter and the answers shown.
  - **Setup:** buses, runs (rename/reorder) and checklist questions.
  - **Correct dialog:** time pickers plus a required reason.
