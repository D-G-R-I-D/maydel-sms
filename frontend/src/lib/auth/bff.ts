import "server-only";
import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/config";
import { toProblem, unreachableProblem, type Problem } from "@/lib/api/problem";
import type { AuthResponse, MeResponse } from "@/lib/api/types";
import { firstName } from "@/lib/flash";
import { ACCESS_COOKIE, REFRESH_COOKIE, clearSession, setFlash, writeSession } from "./cookies";
import { forwardingHeaders } from "./forwarding";
import { refreshSession, sessionIsDead } from "./refresh";
import { canEnter, homeFor, safeNext } from "./roles";
import { needsRefresh } from "./token";
import { checkSameSite } from "./csrf";

export type ApiReply<T> = { ok: true; status: number; data: T } | { ok: false; problem: Problem };

/** Server-to-server call to the API, forwarding the browser's IP and User-Agent. */
export async function callApi<T>(
  request: NextRequest,
  path: string,
  init: { method?: string; body?: unknown; accessToken?: string } = {},
): Promise<ApiReply<T>> {
  let res: Response;
  try {
    res = await fetch(`${apiBaseUrl()}${path}`, {
      method: init.method ?? "POST",
      headers: {
        Accept: "application/json",
        ...(init.body !== undefined ? { "Content-Type": "application/json" } : {}),
        ...forwardingHeaders(request.headers),
        ...(init.accessToken ? { Authorization: `Bearer ${init.accessToken}` } : {}),
      },
      body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
      cache: "no-store",
    });
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
  const body: unknown = res.status === 204 ? null : await res.json().catch(() => null);
  if (res.ok) return { ok: true, status: res.status, data: body as T };
  return { ok: false, problem: toProblem(res.status, body) };
}

/** CSRF guard for the BFF's POST routes: 403 problem unless the request is same-site (see {@link checkSameSite}). */
export function crossSiteRejection(request: NextRequest): NextResponse | null {
  const check = checkSameSite(request.headers);
  if (check.ok) return null;

  // Say exactly what was refused, so a proxy domain missing from TRUSTED_ORIGINS is obvious in the server log.
  const hosts = check.hosts.join(", ") || "(none)";
  console.warn(`[bff] Cross-site request blocked: Origin=${check.origin} Hosts=${hosts}`);
  return problemResponse({
    status: 403,
    title: "Cross-site request blocked.",
    detail: process.env.NODE_ENV === "production" ? undefined : `Origin ${check.origin} doesn't match this server (${hosts}). If it's yours, add it to TRUSTED_ORIGINS in frontend/.env.local and restart.`,
  });
}

export async function readJson(request: NextRequest): Promise<Record<string, unknown> | null> {
  if (!request.headers.get("content-type")?.includes("application/json")) return null;
  const body: unknown = await request.json().catch(() => null);
  return typeof body === "object" && body !== null && !Array.isArray(body) ? (body as Record<string, unknown>) : null;
}

export function str(value: unknown): string {
  return typeof value === "string" ? value : "";
}

export function problemResponse(problem: Problem): NextResponse {
  return NextResponse.json(problem, { status: problem.status, headers: { "Content-Type": "application/problem+json" } });
}

export function badRequest(): NextResponse {
  return problemResponse({ status: 400, title: "The request was not understood.", code: "validation" });
}

/** Where to send a user after a successful sign-in or password change. */
export function landingFor(user: MeResponse, next?: string | null): string {
  if (user.mustChangePassword) return "/change-password";
  const wanted = safeNext(next);
  return wanted && canEnter(wanted.split("?")[0], user.roles) ? wanted : homeFor(user.roles);
}

/** Completes a sign-in: tokens go into httpOnly cookies, only the user goes to the browser. */
export function signedInResponse(auth: AuthResponse, next?: string | null): NextResponse {
  const response = NextResponse.json({ user: auth.user, redirectTo: landingFor(auth.user, next) });
  writeSession(response.cookies, auth);
  if (!auth.user.mustChangePassword) setFlash(response.cookies, { kind: "signed-in", name: firstName(auth.user.fullName) });
  return response;
}

const sessionEnded: Problem = { status: 401, title: "Your session has ended. Please sign in again.", code: "auth.session_expired" };

/**
 * Calls a bearer endpoint on behalf of the browser. Refreshes first when the access token is
 * missing or about to expire, or once after a 401 without a code — never more than one refresh.
 * `finish` applies any cookie changes from that refresh to the outgoing response.
 */
export async function withSession<T>(
  request: NextRequest,
  call: (accessToken: string) => Promise<ApiReply<T>>,
): Promise<{ reply: ApiReply<T>; finish: (response: NextResponse) => NextResponse }> {
  let access = request.cookies.get(ACCESS_COOKIE)?.value;
  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  let refreshedAuth: AuthResponse | null = null;
  let dead = false;
  let refreshed = false;

  const tryRefresh = async (): Promise<boolean> => {
    refreshed = true;
    if (!refreshToken) return false;
    const outcome = await refreshSession(refreshToken, forwardingHeaders(request.headers));
    if (outcome.ok) {
      refreshedAuth = outcome.auth;
      access = outcome.auth.accessToken;
      return true;
    }
    if (sessionIsDead(outcome.problem)) dead = true;
    return false;
  };

  if (needsRefresh(access)) await tryRefresh();

  let reply: ApiReply<T> = access ? await call(access) : { ok: false, problem: sessionEnded };
  if (!reply.ok && reply.problem.status === 401 && !reply.problem.code && !refreshed && (await tryRefresh()) && access) {
    reply = await call(access);
  }

  const finish = (response: NextResponse) => {
    if (dead) clearSession(response.cookies);
    else if (refreshedAuth) writeSession(response.cookies, refreshedAuth);
    return response;
  };
  return { reply, finish };
}
