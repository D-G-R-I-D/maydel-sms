import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** Entrance exam results CSV (E11.7), proxied from the API. */
export async function GET(request: NextRequest) {
  const sp = request.nextUrl.searchParams;
  const sessionId = sp.get("sessionId") ?? "";
  const classLevelId = sp.get("classLevelId");
  if (!GUID.test(sessionId) || (classLevelId && !GUID.test(classLevelId))) return new NextResponse("Not found.", { status: 404 });
  const q = new URLSearchParams({ sessionId });
  if (classLevelId) q.set("classLevelId", classLevelId);
  return proxyBinaryGet(request, `/api/admissions/results.csv?${q.toString()}`);
}
