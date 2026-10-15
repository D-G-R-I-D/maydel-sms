# Handoff → Frontend: E12.3 (part 1) — Email report cards to parents on publish

> Backend status as of 2026-10-15. Contract: `contracts/openapi.json` (run `npm run api:types`).

## What exists now
- **Publishing queues an email per guardian.** Publishing a class's reports queues one email for each guardian of each published student who has an email on record. The report-card PDF is attached at send time.
- **Outbox with retries:**
  - Emails are written in the same transaction as the publish, so a mail-provider outage never blocks or undoes publishing.
  - A background worker sends them, retrying after 1, 2, 4 and 8 minutes, then marking the email **Failed** after 5 attempts.
- **Safety:**
  - The PDF is generated only while the report is still published. If it's unpublished before sending, the email is **Skipped**, so drafts are never mailed.
  - Publishing again **supersedes** unsent emails for the same report; only the newest copy is sent.
- **Providers (configuration only):** `Email:Provider` = `None` | `Pickup` | `Smtp` | `Resend`.
  - **Development uses `Pickup`:** emails are written as `.eml` files in the API's `emails/` folder, and nothing leaves the machine.
  - **Production default is `None`:** emails stay queued until a provider is configured. Secrets come from the environment: `Email__ResendApiKey`, `Email__SmtpPassword`.
- **No opt-out:** results emails are treated as critical, so there's none. Opt-outs for non-critical messages come with announcements and newsletters.

## API changes
| Method & path | Who | Notes |
|---|---|---|
| `POST /api/reports/publish` | Admin | Body gains optional `notifyParents: boolean` (default **true**). The response gains `emailsQueued: number`. |
| `GET /api/reports/emails?classRoomId&termId` | Admin | → `ReportEmailStatus[]`, newest first |
| `POST /api/reports/emails/{emailId}/retry` | Admin | 204; 422 `email.not_failed` unless its status is `Failed`; 404 `email.not_found` |

```ts
type ReportEmailStatus = {
  id: string; studentId: string; student: string; guardian: string; toAddress: string;
  status: "Pending" | "Sent" | "Failed" | "Skipped";
  attempts: number; lastError: string | null; queuedAt: string; sentAt: string | null;
};
```

## UI to build (admin, class reports page)
1. **Publish dialog:** add a checkbox "Email each parent a copy of the report (PDF)", ticked by default. After publishing, show "Published 24 reports · 31 emails queued" (`emailsQueued`). When it's 0 and the box was ticked, add a hint: "No guardians in this class have an email address".
2. **"Parent emails" panel or tab:**
   - A table from `GET /api/reports/emails` with columns Student, Guardian, Email, Status badge (Pending, grey; Sent, green; Failed, red; Skipped, muted), Attempts, Sent at.
   - Show `lastError` in a tooltip or expandable row.
   - Failed rows get a **Retry** button.
   - Poll or refresh every ~15 s while any row is Pending.
3. **Empty state:** "No emails for this term yet."
4. Skipped rows explain themselves via `lastError`, e.g. "Superseded: the report was published again." or "The report is no longer published."
5. **Guardian forms:** a hint under the email field: "Used to send report cards when results are published."

## Not yet
- SMS (Termii / Africa's Talking) and in-app notifications.
- Other triggers: absence, invoices, payments, announcements.
- Per-recipient opt-out for non-critical messages.
- A delivery-status page across all emails.

## Update 2026-10-15: publish only what isn't published yet (fix/publish-once)
- `POST /api/reports/publish` now publishes only reports that are **not yet published**.
  - Already-published reports keep their publication date. Only their position and class average are refreshed, because late classmates can shift them.
  - Their parents are **not** emailed again.
- When nothing is left to publish it returns **422 `report.already_published`**. Pressing Publish twice therefore never re-sends anything.
- `PublishResult` gains `alreadyPublished: number`. `published` now counts only the reports newly published by this call.
- Emails go only to guardians of newly published students. After Unpublish → fix → Publish, that student counts as new again, so their parents get the corrected copy.

### UI changes
1. **Class reports header:** use `publishedCount` / `draftCount` from `GET /api/reports/class`.
   - **All published** (`draftCount === 0 && publishedCount > 0`): replace the Publish button with a status, "✓ All N reports published · {latest publishedAt}". Keep Unpublish.
   - **Some drafts:** "Publish M remaining" (M = draft rows that have scores, i.e. `subjectsScored > 0`). Show the already-published count beside it.
2. **Confirm dialog:** "Publish M reports? Parents of these M students will be emailed" (when the box is ticked). Disable the confirm button while submitting.
3. **On 422 `report.already_published`:** refresh the page data and show the problem `detail` as an info toast, not an error.
4. **After success:** "Published M · K already published · E emails queued".
