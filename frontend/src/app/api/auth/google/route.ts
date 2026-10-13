import type { NextRequest } from "next/server";
import type { AuthResponse } from "@/lib/api/types";
import { badRequest, callApi, crossSiteRejection, problemResponse, readJson, signedInResponse, str } from "@/lib/auth/bff";

/** Forwards the Google Identity Services credential; the API only signs in existing accounts. */
export async function POST(request: NextRequest) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const body = await readJson(request);
  if (!body) return badRequest();

  const reply = await callApi<AuthResponse>(request, "/api/auth/google", { body: { idToken: str(body.credential) } });
  if (!reply.ok) return problemResponse(reply.problem);
  return signedInResponse(reply.data, str(body.next) || null);
}
