# ADR-005 — Phase 0 platform decisions (sessions, gates, contract, drafts)

- **Status:** Accepted (2026-10-12)
- **Builds on:** ADR-002 (BFF auth), ADR-004 (code organisation)

Smaller decisions made while building Phase 0 (E0, E1.1–E1.4, E1.6, E14.1). Each one is small, but a future reader would otherwise have to rediscover it from the code.

## 1. Refresh-token reuse grace window (20 s)
Refresh tokens rotate on every use, and presenting a revoked token revokes the whole family (theft detection). With a BFF, two server requests can legitimately refresh with the same cookie at almost the same moment, which would log the user out at random. **Decision:** a token revoked *by rotation* may be presented again within 20 seconds, and that issues another pair in the same family. After 20 s, or for tokens revoked by logout or sign-out-everywhere, reuse is treated as theft. The BFF should still de-duplicate refreshes per request. `RefreshToken.RevokedReason` records why each token died.

## 2. Temporary passwords are enforced by the API, not only the UI (E1.4)
Access tokens of users with `MustChangePassword` carry `pwd_change: "true"`. `PasswordChangeGate` middleware answers every route outside `/api/auth/*` with `403` and `code: "auth.password_change_required"`. After a change, the new token omits the claim.

## 3. Secure-by-default authorization
`FallbackPolicy = RequireAuthenticatedUser`. An integration test enumerates all endpoints and fails if any route lacks a policy, or allows anonymous access outside `/api/public`, `/api/auth`, `/api/files`, `/api/webhooks`, `/health`, `/openapi`, `/scalar`.

## 4. Error shape: `TypedResults` with `ProblemHttpResult`
ADR-004's example union (`Results<Ok<T>, NotFound<ProblemDetails>, …>`) can't be produced by a single generic `ToHttpResult()`. **Decision:** endpoints return `Results<Ok<T>, ProblemHttpResult>` (or `NoContent`), and each declares its error statuses with `.ProducesProblems(400, 401, …)`, so the OpenAPI contract still lists every response. Every problem carries `code` (stable, machine-readable) and `traceId`. Validation problems add `errors` keyed by camelCase field.

## 5. Transactions and `INonTransactional`
The transaction decorator commits on success and rolls back on a failed `Result`. Sign-in, refresh and logout opt out with `INonTransactional`, because their failure paths must persist side effects (the lockout counter, family revocation).

## 6. Append-only audit at the database level
The `AuditLogAppendOnly` migration adds triggers that reject `UPDATE`/`DELETE`/`TRUNCATE` on `AuditLogs` for every role, so the guarantee doesn't depend on deployment hygiene. Production should *also* revoke those privileges from the app role.

## 7. Trusting forwarded headers
Rate limiting and audit IPs use `RemoteIpAddress`. `X-Forwarded-For` is honoured only from `ForwardedHeaders:KnownNetworks` / `KnownProxies` (default: loopback only). In compose, that's the Docker network. Trusting every proxy would let any client spoof its IP past the rate limiter.

## 8. Contract export and handoffs
`dotnet build` writes `contracts/openapi.json`, and CI fails if the committed copy is stale. The app detects the build-time generator (`GetDocument.Insider`) and skips secrets validation and migrations for it. At the end of each milestone, the backend writes `docs/handoffs/<milestone>.md`: a paste-ready brief for the frontend session (§8.4 of the architecture doc).

## 9. Draft module code parked in `backend/drafts/`
The first-draft endpoint files (academics, students, staff, assessment, public, files, report calculator) didn't compile, and they predate ADR-004. They now live in `backend/drafts/` outside every project, as reference for the Phase 1 slices: their access rules and report calculation carry over. Delete each file once its module has been rebuilt.

## 10. No CORS
The browser never calls the API directly (BFF), so CORS is not enabled. Add an explicit allow-list only if that changes.
