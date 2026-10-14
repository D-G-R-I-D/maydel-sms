import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** A parent's/student's published report card PDF, via the portal API (another family's child → 404). */
export async function GET(request: NextRequest, { params }: { params: Promise<{ studentId: string; termId: string }> }) {
  const { studentId, termId } = await params;
  if (!GUID.test(studentId) || !GUID.test(termId)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/portal/children/${studentId}/results/${termId}/pdf`);
}
