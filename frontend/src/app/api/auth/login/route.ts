import type { NextRequest } from "next/server";
import type { AuthResponse } from "@/lib/api/types";
import { badRequest, callApi, crossSiteRejection, problemResponse, readJson, signedInResponse, str } from "@/lib/auth/bff";

export async function POST(request: NextRequest) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const body = await readJson(request);
  if (!body) return badRequest();

  const reply = await callApi<AuthResponse>(request, "/api/auth/login", {
    body: { userName: str(body.userName), password: str(body.password) },
  });
  if (!reply.ok) return problemResponse(reply.problem);
  return signedInResponse(reply.data, str(body.next) || null);
}
