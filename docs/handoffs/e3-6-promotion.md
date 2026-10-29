# E3.6 End-of-session promotion: frontend handoff

> Backend shipped 2026-10-29. Admins only. The contract is in `contracts/openapi.json` (tag **Promotions**).

## How it works (for the school)
1. **Opens in the third term.** From the day the session's third term starts (or any time after the session ends), an admin opens **Promotion**.
2. **Preview.** It shows one class, or all of them. Every student is pre-filled:
   - **Promote** to the next level's class with the same arm (Year 3 A → Year 4 A), or that level's only class;
   - **Graduate** for the top level (Year 12).
   A student whose session average (the mean of their published term reports this session) is **below 40** is flagged *Review*. It is only a hint: nothing is changed automatically.
3. **Exceptions.** The admin changes individual students: **Repeat** (stays in the same class), **Withdraw**, **Graduate**, or a different class (another arm).
4. **Apply.** One click applies the batch in **one transaction**: all or nothing.
   - **Class capacity** is checked after every move in the batch. "Promote all classes" in one go is therefore the easy path, because Year 4 has moved on to make room for Year 3. Promoting Year 3 alone into a full Year 4 is refused with a clear message.
   - **No double promotion.** Each student can be moved on **once per session**. A second attempt is refused.
   - **Logins.** Graduating or withdrawing turns the student's own login off, just like changing their status.
5. **Undo per class.** This puts the students back in the class with their old status. Anyone changed by hand since the run is left alone and listed.
6. **Manual edits still work.** Changing a student's class or status on their profile works as before, at any time.

**Past report cards keep their class.** Term reports and scores store the class they were earned in, so promoting never changes old results. Fees, timetables and the like follow the student's new class from then on. Create the new session and make its first term current under **Sessions & terms** as usual. Promotion doesn't do that.

## Endpoints
| Method & path | Body / query | Response |
|---|---|---|
| `GET /api/promotions?sessionId=&classRoomId=` | both optional (default: current session, every class with students) | `PromotionPlan` |
| `POST /api/promotions` | `{ sessionId?, decisions: [{ studentId, outcome, toClassRoomId? }] }` | `PromotionSummary { promoted, repeated, graduated, withdrawn }` |
| `POST /api/promotions/undo` | `{ sessionId?, classRoomId }` | `PromotionUndone { undone, skipped: string[] }` |

The `outcome` values are `Promote`, `Repeat`, `Graduate` and `Withdraw`. `toClassRoomId` is required for `Promote` and ignored otherwise.

**`PromotionPlan`** = `{ sessionId, session, canPromote, closedReason?, classes: PromotionClass[] }`

**`PromotionClass`** = `{ classRoomId, name, levelOrder, isFinalLevel, nextClassRoomId?, nextClassName?, students: PromotionStudent[] }`
- When `nextClassRoomId` is null and `isFinalLevel` is false, the next level has several arms and none matches. The admin must pick the class.

**`PromotionStudent`** = `{ studentId, fullName, admissionNumber, status, sessionAverage?, needsReview, suggestedOutcome, suggestedClassRoomId?, done? }`
- `done` = `{ outcome, toClassRoomId, toClassName, at }` once the student has been moved on this session. Show those rows read-only, with the class's **Undo** button.

## Errors
| Status | Code | When |
|---|---|---|
| 422 | `promotion.too_early` / `promotion.no_third_term` | Before the third term starts. Also returned in the plan as `canPromote: false` with `closedReason`. |
| 409 | `promotion.already_done` | A student in the batch was already moved on this session. Undo that class first. |
| 422 | `promotion.class_full` | A class would go over capacity. The message names it. |
| 422 | `promotion.choose_class` / `promotion.same_class` | Promote without a class, or to the same class (use Repeat). |
| 422 | `promotion.not_enrolled` | The student isn't active or suspended in a class. |
| 422 | `promotion.undo_class_full` | Undo would overfill the class. Undo the class that moved into it first. |
| 404 | `promotion.no_session`, `classroom.not_found`, `student.not_found`, `promotion.nothing_to_undo` | |
| 400 | validation | Empty list, a student listed twice, or more than 3,000 students. |

## What the UI should build
**Admin → Academics → Promotion**
- **Header:** the session picker (default current), plus a banner with `closedReason` when `canPromote` is false. Disable Apply then, but keep the preview usable.
- **Class sections:** one per class, as "Year 3 A → Year 4 A", or "→ Graduate" for the final level. Each has a target-class dropdown that changes the whole class at once.
- **Student table:** the name, the session average, and a *Review* badge when `needsReview`. An outcome select (Promote, Repeat, Withdraw, Graduate), plus a class select when Promote.
- **Filters:** "Show only flagged".
- **Actions:** **Apply to this class**, and **Apply to all classes** (the recommended path, with a note about capacity).
  - Before applying, show a confirmation summary: "32 promoted, 2 repeating, 1 withdrawn, 40 graduating".
- **Done classes:** they show what happened, plus an **Undo** button (with a confirmation).
- **Undo result:** list any skipped names in a toast.
