import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** One student's report card PDF, proxied from the API (the browser never calls the API). */
export async function GET(request: NextRequest, { params }: { params: Promise<{ studentId: string; termId: string }> }) {
  const { studentId, termId } = await params;
  if (!GUID.test(studentId) || !GUID.test(termId)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/reports/${studentId}/${termId}/pdf`);
}
