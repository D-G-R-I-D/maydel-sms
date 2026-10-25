# Handoff → Frontend: homework (E17.1)

> Backend status as of 2026-10-24. Contract: `contracts/openapi.json` (run `npm run api:types`).

## Who does what
- **Setting homework.** A **teacher** sets homework for a class and a subject they teach. The class's **form teacher** can set any subject, or general homework with no subject. Admins can do everything.
- **Students** see their class's homework, download the attachments, and **hand in** text and/or up to 5 files. Handing in again replaces the earlier hand-in. A hand-in after the due date is marked late.
- **Parents** see each child's homework and whether it's handed in, seen or returned. Only the student can hand in.
- **Reviewing.** The teacher sees the whole class's hand-ins, opens each one, and marks it **Seen**, or **Returned** with feedback. A returned hand-in can be handed in again.
- **Notifications** (`/api/notifications`) go to the student, if they have a login, and to their guardians:
  - `HomeworkSet` for new homework;
  - `HomeworkReviewed` when a hand-in is seen or returned.

  `linkUrl` is `/portal/homework/{homeworkId}`.

## Files
- **Allowed:** PDF, photos (JPEG/PNG/WebP, re-encoded), and Word/Excel/PowerPoint (`.docx`, `.xlsx`, `.pptx`). Up to 10 MB each.
- Files are recognised by their content, never by the name alone. Anything else returns 400 field `file`.
- **Limits:** 5 attachments per homework, 5 files per hand-in.
- Files are always served as downloads, with the original (cleaned) file name.

## Staff endpoints (admin, teacher)
| Method & path | Body | Response / errors |
|---|---|---|
| `GET /api/homework?classRoomId&subjectId&page&pageSize` | | `PagedResult<HomeworkDto>` for classes you teach (admins: all), latest due first |
| `GET /api/homework/{id}` | | `HomeworkDto`; 404 if it isn't your class |
| `POST /api/homework` | `{ classRoomId, subjectId?, title, instructions?, instructionsHtml?, dueOn, acceptsSubmissions? }` | `HomeworkDto`; 403 `homework.forbidden`, 400 `dueOn` (in the past), 400 `subjectId` (not taught in the class) |
| `PUT /api/homework/{id}` | same | `HomeworkDto` |
| `DELETE /api/homework/{id}` | | 204; 422 `homework.has_submissions` |
| `POST /api/homework/{id}/files` | multipart `file` | `HomeworkDto`; 422 `homework.too_many_files` |
| `DELETE /api/homework/{id}/files/{fileId}` | | `HomeworkDto` |
| `GET /api/homework/{id}/hand-ins` | | `[{ studentId, name, admissionNumber, state, submission? }]`: every active student, sorted by name |
| `PUT /api/homework/{id}/hand-ins/{submissionId}` | `{ status: "Seen" \| "Returned", feedback? }` | `SubmissionDto` |

`HomeworkDto = { id, classRoomId, classRoom, subjectId, subject, title, instructions, instructionsHtml, dueOn, acceptsSubmissions, setBy, files: [{ id, fileName, contentType, size }], createdAt, submitted, classSize }`

`submitted` and `classSize` are set for staff and null in the portal.

## Portal endpoints (parent, student)
| Method & path | Body | Response / errors |
|---|---|---|
| `GET /api/portal/children/{studentId}/homework?open` | | `MyHomework[]`, soonest due first. `open=true` returns only homework due today or later; otherwise it includes the last 60 days |
| `GET /api/portal/children/{studentId}/homework/{homeworkId}` | | `MyHomework` |
| `POST /api/portal/children/{studentId}/homework/{homeworkId}/hand-in` | multipart: `text?` plus `files` (repeat the field, up to 5) | `MyHomework`; 400 `text` (empty), 400 `file`, 403 `homework.students_only`, 422 `homework.no_submissions` / `homework.too_many_files` |

- `MyHomework = { homework: HomeworkDto, state: Missing | Submitted | Seen | Returned, isOverdue, submission? }`.
- `submission = { id, text, files, submittedAt, isLate, status, feedback, reviewedAt }`.

**Downloads** (any signed-in user; the server checks the row, and anyone else gets 404):
- `GET /api/homework/{id}/files/{fileId}`: the homework's attachments.
- `GET /api/homework/{id}/hand-ins/{submissionId}/files/{fileId}`: a hand-in's files.

## UI to build
- **Teacher, Homework:**
  - **List:** class and subject filters, due date, and an "8/30 handed in" bar.
  - **New/edit:** the rich editor for instructions, attachments, due date, and an "Hand in online" switch.
  - **Hand-ins page:** the roster with state pills; open one to see the text and files; Seen / Return with feedback.
- **Student dashboard:**
  - a "Homework" card (due soon, overdue in red);
  - the homework page with instructions, downloads and a hand-in form (text area, file picker up to 5, upload progress);
  - after handing in, the status and any feedback.
- **Parent portal:** a homework tab per child, read-only, with state pills and feedback.
