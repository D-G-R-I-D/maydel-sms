import { NextResponse, type NextRequest } from "next/server";
import type { AuthResponse } from "@/lib/api/types";
import { setFlash, writeSession } from "@/lib/auth/cookies";
import { badRequest, callApi, crossSiteRejection, landingFor, problemResponse, readJson, str, withSession } from "@/lib/auth/bff";

/** Changes the password and replaces both cookies with the returned pair (the API signs out other devices). */
export async function POST(request: NextRequest) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const body = await readJson(request);
  if (!body) return badRequest();

  const { reply, finish } = await withSession(request, (accessToken) =>
    callApi<AuthResponse>(request, "/api/auth/change-password", {
      accessToken,
      body: { currentPassword: str(body.currentPassword), newPassword: str(body.newPassword) },
    }),
  );
  if (!reply.ok) return finish(problemResponse(reply.problem));

  const response = NextResponse.json({ user: reply.data.user, redirectTo: landingFor(reply.data.user) });
  writeSession(response.cookies, reply.data);
  setFlash(response.cookies, { kind: "password-changed" });
  return response;
}
