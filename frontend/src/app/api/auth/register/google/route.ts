import type { NextRequest } from "next/server";
import type { AuthResponse } from "@/lib/api/types";
import { badRequest, callApi, crossSiteRejection, problemResponse, readJson, signedInResponse, str } from "@/lib/auth/bff";

/**
 * E11 follow-up: buy an admission form with a Google account. The API verifies the Google credential,
 * creates the applicant account for that email (no password) and returns a session, set as cookies here.
 */
export async function POST(request: NextRequest) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const body = await readJson(request);
  if (!body) return badRequest();

  const reply = await callApi<{ session: AuthResponse }>(request, "/api/public/admissions/register/google", {
    body: {
      idToken: str(body.credential),
      parentName: str(body.parentName),
      phone: str(body.phone),
      childFirstName: str(body.childFirstName),
      childLastName: str(body.childLastName),
      childDateOfBirth: str(body.childDateOfBirth) || null,
      classLevelId: str(body.classLevelId),
      sessionId: str(body.sessionId),
      website: str(body.website), // honeypot
    },
  });
  if (!reply.ok) return problemResponse(reply.problem);
  return signedInResponse(reply.data.session, "/admissions");
}
