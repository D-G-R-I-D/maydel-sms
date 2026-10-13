import { NextResponse, type NextRequest } from "next/server";
import { REFRESH_COOKIE, clearSession, setFlash } from "@/lib/auth/cookies";
import { callApi, crossSiteRejection } from "@/lib/auth/bff";
import { redirectTo } from "@/lib/http/redirect";

/** Revokes the session at the API (best effort) and always clears the cookies. */
export async function POST(request: NextRequest) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;

  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  if (refreshToken) await callApi<null>(request, "/api/auth/logout", { body: { refreshToken } });

  const wantsJson = request.headers.get("accept")?.includes("application/json") ?? false;
  const response = wantsJson ? new NextResponse(null, { status: 204 }) : redirectTo("/login", 303);
  clearSession(response.cookies);
  setFlash(response.cookies, { kind: "signed-out" });
  return response;
}
