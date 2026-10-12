# 04 — Security & Privacy

We hold **children's personal data, academic records and payments**. Security is a feature with acceptance criteria, not an afterthought. No third-party auth platform (Firebase/Auth0) — we own identity, using battle-tested ASP.NET Core Identity primitives rather than hand-rolled crypto.

## 1. Authentication

| Concern | Decision |
|---|---|
| Password storage | ASP.NET Core Identity hasher (PBKDF2-HMAC-SHA512, 100k+ iterations, per-user salt) |
| Password policy | ≥ 10 chars, upper + lower + digit; checked against common-password list (phase 2: HaveIBeenPwned k-anonymity) |
| Brute force | Lockout 5 failures / 15 min per account; rate limit 10 req/min/IP on `/api/auth/*` |
| Enumeration | Identical response for unknown user / wrong password / disabled |
| Access token | JWT HS256, 15 min, claims: `sub`, `role`, `name`, `sstamp`; validated issuer/audience/lifetime/alg |
| Refresh token | 64 random bytes, **SHA-256 hash stored**, 14 days, rotated on every use, **reuse ⇒ whole family revoked** |
| Revocation | Security stamp in token, checked per request → password change, role change, deactivation take effect immediately |
| Temporary passwords | Generated server-side, shown once, `MustChangePassword` forced on first login |
| Google sign-in | Client obtains Google **ID token** (Google Identity Services) → API verifies signature, `aud` = our client id, `iss`, expiry, `email_verified` → maps to an **existing** Parent/Staff account by email → links Google `sub`; a different `sub` later is rejected. Never creates accounts; students excluded. |
| MFA (phase 2) | TOTP for Admin & Bursar |

## 2. Session handling in the browser (BFF)

- Tokens live only in `httpOnly; Secure; SameSite=Lax; Path=/` cookies set by Next.js route handlers. No tokens in `localStorage` or JS memory → XSS cannot exfiltrate sessions.
- Mutations go through same-origin Server Actions / route handlers (Next.js verifies `Origin` for Server Actions) → CSRF mitigated; `SameSite=Lax` as second layer.
- Middleware refreshes the access token silently when < 2 min remain.

## 3. Authorization

Role policies at the endpoint **plus** row-level checks in code (`Access` helpers). UI hiding is never relied upon.

| Capability | Admin | Teacher | Bursar | Parent | Student |
|---|:-:|:-:|:-:|:-:|:-:|
| School setup, staff, publish results, audit | ✅ | | | | |
| View students list/profile | ✅ | ✅ | ✅ | own children | self |
| Create/edit students, guardians, logins | ✅ | | | | |
| Enter scores | ✅ | assigned class-subjects / form class | | | |
| Report comments | ✅ (principal) | form class | | | |
| View report card | ✅ draft+published | ✅ | | published, own children | published, self |
| Attendance register | ✅ | form class (same day) | | view own | view self |
| Fees setup, invoices, payments | ✅ | | ✅ | view own + pay | view self |
| Announcements | ✅ | create for own class (phase 2) | | read | read |
| Admissions pipeline, CMS | ✅ | | | | |

Every "forbidden" cell has an integration test (Definition of Done).

## 4. Application security controls (OWASP ASVS L2 as target)

- Input validation server-side on every DTO (length, range, format); domain invariants in the model.
- EF Core parameterised queries only; no raw SQL string concatenation.
- File uploads: allow-list MIME **and magic bytes**, 12 MB cap, server-generated names, stored outside web root; student photos/documents served only through authorised endpoints; images re-encoded (phase 2) to strip EXIF/GPS.
- Problem details without stack traces; trace id for support.
- Security headers: HSTS, `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, strict CSP on API; Next.js CSP with nonces for the web.
- CORS: explicit allow-list (only needed if the browser calls the API directly; BFF calls are server-to-server).
- Webhooks (Paystack): HMAC-SHA512 signature verification, idempotency by reference, amount re-verified via API.
- Secrets: env vars / cloud secret store; `dotnet user-secrets` locally; secret scanning in CI.
- Dependencies: Dependabot + `dotnet list package --vulnerable` + `npm audit` in CI.
- Audit log: every data change, actor, IP, redacted diff — append-only (no update/delete endpoints; DB role for app lacks `DELETE` on the table in prod).

## 5. Privacy — Nigeria Data Protection Act 2023

- **Lawful basis & consent:** admission form captures guardian consent for processing and for photo use on the website (separate opt-in).
- **Minimisation:** collect only fields used by the school; medical notes visible to Admin + form teacher only (phase 2 field-level rule).
- **Data subject rights:** admin can export a student's full record (JSON/PDF) and process correction requests.
- **Retention:** withdrawn/graduated students' records archived; personal data purged after the retention period the school sets (default 7 years for academic records).
- **Breach response:** runbook in `docs/runbooks/` — contain, assess, notify NDPC within 72 h where required, notify affected families.
- **Processors:** hosting, email/SMS and payment providers listed in the privacy notice.

## 6. Threat model (top risks)

| Threat | Mitigation |
|---|---|
| Parent views another child's results (IDOR) | Row-level `Access` checks + tests on every portal endpoint |
| Teacher alters grades after publication | Publish lock + audit trail |
| Credential stuffing | Lockout, rate limit, MFA for privileged roles |
| Stolen refresh token | Rotation + reuse detection, httpOnly cookies |
| Malicious upload | Magic-byte validation, re-encode, private storage |
| Fake payment confirmation | Server-side verification with gateway + signed webhook |
| Insider data export | Audit log, least privilege, admin-only exports logged |
