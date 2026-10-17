import type { NextRequest } from "next/server";
import type { AuthResponse } from "@/lib/api/types";
import { badRequest, callApi, crossSiteRejection, problemResponse, readJson, signedInResponse, str } from "@/lib/auth/bff";

/**
 * E11: buy an admission form. Creates the family's applicant account through the API, then signs it
 * in with the same email and password so the family lands in /admissions with httpOnly cookies.
 */
export async function POST(request: NextRequest) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const body = await readJson(request);
  if (!body) return badRequest();

  const email = str(body.email);
  const password = str(body.password);
  const registered = await callApi<unknown>(request, "/api/public/admissions/register", {
    body: {
      parentName: str(body.parentName),
      email,
      phone: str(body.phone),
      password,
      childFirstName: str(body.childFirstName),
      childLastName: str(body.childLastName),
      childDateOfBirth: str(body.childDateOfBirth) || null,
      classLevelId: str(body.classLevelId),
      sessionId: str(body.sessionId),
      website: str(body.website), // honeypot: real people leave it empty
    },
  });
  if (!registered.ok) return problemResponse(registered.problem);

  const login = await callApi<AuthResponse>(request, "/api/auth/login", { body: { userName: email, password } });
  if (!login.ok) return problemResponse(login.problem);
  return signedInResponse(login.data, "/admissions");
}
