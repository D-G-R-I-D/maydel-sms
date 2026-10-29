# Handoff → Frontend: communication book (E17.2)

> Backend status as of 2026-10-25. Contract: `contracts/openapi.json` (run `npm run api:types`).

## What it is
- **One running notebook per student** between:
  - the **school**: the class's form teacher, and admins;
  - the **family**: the student's guardians, and the student.
- **Notes:** short text (up to 4,000 characters) with an optional file. The same file types as homework apply: PDF, photos, docx/xlsx/pptx, up to 10 MB.
- **Corrections:** a note can be corrected by its author for **15 minutes**; after that, add a follow-up.
- **Unread:** each person has their own read mark, so lists show unread counts. Writing a note marks the book read for you.
- **Notifications** (`/api/notifications`, kind `BookEntry`):
  - a school note goes to the guardians and the student;
  - a family note goes to the form teacher and the other guardians (both parents see the whole conversation).

  Links: family `/portal/book/{studentId}`, staff `/books/{studentId}`. Map them to your routes.
- **No access:** subject teachers who aren't the form teacher, other families, and bursars all get 404.

## Endpoints
| Method & path | Who | Body | Response / errors |
|---|---|---|---|
| `GET /api/books?classRoomId` | form teacher, admin | | `BookSummary[]` for the class. Admins may omit `classRoomId` to get every book with unread notes for them. 403 `book.forbidden` if not the form teacher |
| `GET /api/portal/books` | parent, student | | `BookSummary[]`: your children's books, or your own |
| `GET /api/books/{studentId}?before` | anyone who may open it | | `BookPage`: the latest 50 notes, **oldest first** (chat order). `hasOlder`: call again with `before` = the first note's `createdAt` |
| `POST /api/books/{studentId}/entries` | same | multipart: `body` and/or `file` | `BookEntryDto`; 400 `body` / `file` |
| `PUT /api/books/{studentId}/entries/{entryId}` | the author | `{ body }` | `BookEntryDto`; 403 `book.not_author`, 422 `book.edit_window` |
| `POST /api/books/{studentId}/read` | same | | 204: call it when the book is open |
| `GET /api/books/{studentId}/entries/{entryId}/file` | same | | download |

- `BookSummary = { studentId, name, admissionNumber, classRoom, lastEntryAt, lastEntry (≤120 chars), unread }`.
- `BookEntryDto = { id, studentId, authorName, side: School | Family, isMine, body, attachment?: { id, fileName, contentType, size }, createdAt, editedAt, canEdit }`.

## UI to build
- **Teacher (form teacher):** "Communication books" in the class menu.
  - **List:** students with unread badges and the last note.
  - **Book view:** a chat-like thread with school notes on one side and family notes on the other. A composer with a text area, an attach button and Send. "Edit" appears while `canEdit`; show "edited" when `editedAt` is set.
  - Call `/read` on open.
- **Parent portal:** a "Communication book" tab per child, the same thread view, with the unread badge on the child card.
- **Student portal:** "My communication book", the same view.
- **Admin:** a "Books with unread notes" inbox via `GET /api/books` with no class.

## Evening email summary (added 2026-10-29)
- From **18:00 WAT**, anyone with unread notes and an email address gets **one email a day**: "New in the communication book".
  - It lists the unread notes per child, up to the last 3 per child with author and text, and links to the portal.
  - It goes to the same people as the portal notifications (the other side of the book), skipping notes they've already read.
  - It covers only notes that arrived since their last summary.
- **No UI needed.** Portal notifications still arrive as each note is written.
