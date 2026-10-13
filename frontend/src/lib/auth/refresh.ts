import { apiBaseUrl } from "@/lib/api/config";
import { toProblem, unreachableProblem, type Problem } from "@/lib/api/problem";
import type { AuthResponse } from "@/lib/api/types";

export type RefreshOutcome = { ok: true; auth: AuthResponse } | { ok: false; problem: Problem };

/**
 * Prefetches and parallel requests can all decide to refresh at once with the same cookie.
 * Share one in-flight/just-finished refresh per token for a few seconds (inside the API's
 * 20 s reuse grace window) so we spend one rate-limit slot and rotate the family once.
 */
const SHARE_MS = 10_000;
const inflight = new Map<string, { at: number; promise: Promise<RefreshOutcome> }>();

export function refreshSession(refreshToken: string, forwarding: Record<string, string>): Promise<RefreshOutcome> {
  const now = Date.now();
  for (const [key, entry] of inflight) if (now - entry.at > SHARE_MS) inflight.delete(key);

  const existing = inflight.get(refreshToken);
  if (existing) return existing.promise;

  const promise = callRefresh(refreshToken, forwarding);
  inflight.set(refreshToken, { at: now, promise });
  return promise;
}

async function callRefresh(refreshToken: string, forwarding: Record<string, string>): Promise<RefreshOutcome> {
  let res: Response;
  try {
    res = await fetch(`${apiBaseUrl()}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json", ...forwarding },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
    });
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
  const body: unknown = await res.json().catch(() => null);
  if (res.ok) return { ok: true, auth: body as AuthResponse };
  return { ok: false, problem: toProblem(res.status, body) };
}

/** Only a definitive 400/401 means the session is gone; 429 / 5xx / network may be transient. */
export function sessionIsDead(problem: Problem): boolean {
  return problem.status === 400 || problem.status === 401;
}
