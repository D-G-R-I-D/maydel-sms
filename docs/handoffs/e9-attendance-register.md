# Handoff → Frontend: E9.1–E9.2 — Daily attendance register

> Backend status as of 2026-10-15. Contract: `contracts/openapi.json` (run `npm run api:types`).

## What exists now
- **Daily register per class.**
  - Every active student defaults to **Present**. Mark someone Absent, Late or Excused, with an optional note (≤ 200 chars).
  - A save with no entries means "all present": students left out are recorded Present.
  - One record per student per day; saving again updates it.
- **Who and when.** "Today" is West Africa Time (UTC+1).
  - **Form teacher:** today only.
  - **Admin:** any earlier school day too.
  - **Nobody:** future dates. Other teachers of the class can view but not edit.
- **School days only.** No register at weekends, during the term's mid-term break, or outside any term. `GET` returns `problem` (a user-facing reason) and `canEdit: false`.
- **Report cards use it:**
  - When attendance hasn't been typed in on the Reports page, the report shows the register's totals: days opened = days this class's register was taken, present = present + late.
  - Publishing copies those totals onto the report and freezes them, so **the form teacher no longer has to enter attendance by hand**. Manual entry still works and wins if used.
- **Locked after publishing:** once a student's term report is published, the register refuses changes for them (422 `attendance.report_published`), and their entries show `locked: true`.

## Endpoints (Admin, Teacher)
| Method & path | Who | Response | Errors |
|---|---|---|---|
| `GET /api/attendance/register?classRoomId&date?` | Admin; teachers of the class | `ClassRegister` (`date` defaults to today) | 403 `attendance.not_allowed`, 404 |
| `PUT /api/attendance/register` `{ classRoomId, date, entries: [{ studentId, status, note? }] }` | Form teacher (today), Admin | `ClassRegister` | 400 (`entries[i].studentId`, …); 403 `attendance.not_form_teacher`; 422 `attendance.past_day` / `attendance.future` / `attendance.weekend` / `attendance.mid_term` / `attendance.not_in_term` / `attendance.report_published` |
| `GET /api/attendance/summary?classRoomId&termId` | Admin; teachers of the class | `ClassAttendanceSummary` | 403, 404 |

```ts
type AttendanceStatus = "Present" | "Absent" | "Late" | "Excused";
type ClassRegister = {
  classRoomId: string; classRoom: string; date: string; termId: string | null; term: string | null;
  problem: string | null;          // why there's no register that day; null on school days
  taken: boolean; canEdit: boolean;
  present: number; absent: number; late: number; excused: number;
  entries: { studentId: string; admissionNumber: string; name: string; status: AttendanceStatus; note: string | null;
             recorded: boolean; locked: boolean }[];
};
type ClassAttendanceSummary = {
  classRoomId: string; classRoom: string; termId: string; term: string; daysOpened: number;
  rows: { studentId: string; admissionNumber: string; name: string; daysOpened: number;
          present: number; absent: number; late: number; excused: number; percentage: number | null }[];
};
```

## UI to build
1. **Attendance (`/admin/attendance`), nav item ready:**
   - Teachers land on today's register for their form class; admins pick a class.
   - A date picker (default today), with prev and next day buttons.
2. **Register screen, phone-first (teachers use phones in class):**
   - One row per student: name, then a 4-way segmented control P / A / L / E, large enough to tap.
   - An optional note field appears when the status isn't Present.
   - A sticky footer shows the counts and a **"Save register"** button. Before the first save, show a big **"All present"** shortcut.
   - Show "Saved at HH:MM" when `taken`.
   - When `canEdit` is false, show read-only, with a hint: "Only an administrator can change earlier days", or the `problem` text for weekends and mid-term.
   - `locked` rows are disabled, with a hint: "Report published".
3. **Term summary (tab):** a table from `/summary` with days opened, present, absent, late, excused and %. Highlight below 90% (amber) and below 75% (red).
4. **Reports page:** attendance is now pre-filled from the register. Show "From register: N days" when the manual entry is empty.
5. **Dashboard:** link the "Attendance" tile (it's been "Coming soon").
6. Drafts (`useDraft`) apply to the register too: unsaved marks survive a reload.

## Not yet
- Absence notifications to guardians (E9.3, via the E12 outbox).
- The parent portal attendance tab (E6.4) will use these totals.
