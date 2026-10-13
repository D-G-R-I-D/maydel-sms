import { NextResponse, type NextRequest } from "next/server";
import { REFRESH_COOKIE, REFRESHED_COOKIE, clearSession, markRefreshed, writeSession } from "@/lib/auth/cookies";
import { crossSiteRejection, problemResponse } from "@/lib/auth/bff";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import { refreshSession, sessionIsDead } from "@/lib/auth/refresh";
import { safeNext } from "@/lib/auth/roles";

/** Explicit refresh for client code: rotates the pair and returns only the user. */
export async function POST(request: NextRequest) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  if (!refreshToken) {
    return problemResponse({ status: 401, title: "Your session has ended. Please sign in again.", code: "auth.session_expired" });
  }

  const outcome = await refreshSession(refreshToken, forwardingHeaders(request.headers));
  if (!outcome.ok) {
    const response = problemResponse(outcome.problem);
    if (sessionIsDead(outcome.problem)) clearSession(response.cookies);
    return response;
  }
  const response = NextResponse.json({ user: outcome.auth.user });
  writeSession(response.cookies, outcome.auth);
  return response;
}

/**
 * Fallback for Server Components after an API 401: one refresh, then back to `next`.
 * If that already happened moments ago (marker cookie) the session is unusable, so → /login.
 */
export async function GET(request: NextRequest) {
  const next = safeNext(request.nextUrl.searchParams.get("next")) ?? "/";
  const loginUrl = (reason: string) => {
    const url = new URL("/login", request.url);
    if (next !== "/") url.searchParams.set("next", next);
    url.searchParams.set("reason", reason);
    return url;
  };
  const expired = () => {
    const response = NextResponse.redirect(loginUrl("expired"), 303);
    clearSession(response.cookies);
    return response;
  };

  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  if (!refreshToken || request.cookies.has(REFRESHED_COOKIE)) return expired();

  const outcome = await refreshSession(refreshToken, forwardingHeaders(request.headers));
  if (!outcome.ok) {
    if (sessionIsDead(outcome.problem)) return expired();
    return NextResponse.redirect(loginUrl(outcome.problem.status === 429 ? "busy" : "unavailable"), 303);
  }
  const response = NextResponse.redirect(new URL(next, request.url), 303);
  writeSession(response.cookies, outcome.auth);
  markRefreshed(response.cookies);
  return response;
}
