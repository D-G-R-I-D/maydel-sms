# Handoff → Frontend: follow-ups from user feedback (2026-10-19)

> Contract: `contracts/openapi.json` (run `npm run api:types`).

## 1. Sign up with Google when buying the admission form
- `POST /api/public/admissions/register/google` (anonymous, rate-limited):
  - Body: `{ idToken, parentName, phone, childFirstName, childLastName, childDateOfBirth?, classLevelId, sessionId, website: "" }`.
  - Returns `{ session: AuthResponse, application: ApplicationDto }`.
- The verified Google email becomes the applicant account (no password) and is **signed in**. Set the cookies from `session` exactly like a normal login.
- Errors:
  - 409 `admission.email_in_use`: "You already have an account. Sign in (with Google or your password) and apply from your account."
  - 401 `auth.google_invalid` / `auth.google_email_unverified`.
  - 404 `auth.google_disabled`.
  - 422 `admission.closed`.
  - 400 for fields.
- **Later sign-ins:** the normal `POST /api/auth/google` works for this account. On enrolment the same account becomes the parent account.
- **UI:** a "Continue with Google" button on `/admissions/apply`. The form then only needs parent name, phone and child details; skip email and password.

## 2. Entry term (2nd/3rd-term intakes)
- `PUT /api/admissions/settings` accepts a new `entryTermId?`, a term of that session; null means the first term (400 `entryTermId` otherwise).
- `AdmissionSettingsDto` adds `entryTermId`, `entryTerm` (e.g. "Second Term 2027/2028") and `entryStartsOn`.
- What it affects:
  - **Offer letters:** they show the entry term's start as the resumption date.
  - **Enrolment:** it admits from that term's start, so its invoice bills new-intake items.
  - **Per child:** `POST …/applications/{id}/enrol` accepts `entryTermId?` to override for one child.
- **UI:** an "Admits start in" term select in admissions settings, and an optional term on the Enrol dialog (default = settings).

## 3. Enrolment tells the admin about the parent login
- `EnrolmentResult` adds:
  - `parentLogin`: `"Linked"` | `"PhoneBelongsToAnotherAccount"` | `"NoGuardian"`;
  - `message`: for the admin when not linked.
- **UI:** on anything but `Linked`, show the message as a warning with a link to Students → Guardians.

## 4. Show the computer-marked score on hand-in (per test)
- `TestInput` / `TestDetail` add `showScoreOnSubmit` (default false).
- When it's on, after hand-in (before release):
  - `AttemptPaper.score` is the computer-marked total;
  - item `score`s are shown for auto-marked questions;
  - `scoreIsProvisional` is true while theory still needs the teacher;
  - `StudentTestDto.score` and `scoreIsProvisional` follow the same rule.
- Release still shows final marks and comments.
- **UI:** a "Show score on hand-in" toggle in the test header. The student result card shows "Score so far: 12 / 20 (theory still being marked)" when provisional.

## 5. Notify passed: already-emailed count
- `POST /api/admissions/results/notify-passed` → `{ queued, alreadyNotified }`.
- Toast: "Emailed N families (they'll arrive within minutes)". When N = 0: "Everyone who passed (M) has already been emailed."

## 6. AI question writing: free fallback
- The server uses **Claude** when an Anthropic key is set, otherwise **Google Gemini (free tier)** when a Gemini key is set.
- `quota.available` is true when either key is present. No API change.
- The free tier can be busy; it fails with a friendly message and costs nothing.

## Also fixed
- Test sections keep the teacher's order after publishing (#84).
