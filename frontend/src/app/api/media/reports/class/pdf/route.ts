import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** Whole-class report PDF (one page per student), proxied from the API. */
export async function GET(request: NextRequest) {
  const classRoomId = request.nextUrl.searchParams.get("classRoomId") ?? "";
  const termId = request.nextUrl.searchParams.get("termId") ?? "";
  if (!GUID.test(classRoomId) || !GUID.test(termId)) return new NextResponse("Not found.", { status: 404 });
  const q = new URLSearchParams({ classRoomId, termId });
  return proxyBinaryGet(request, `/api/reports/class/pdf?${q.toString()}`);
}
