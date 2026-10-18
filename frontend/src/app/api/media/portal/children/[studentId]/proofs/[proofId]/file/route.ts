import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** A parent's own uploaded proof (E10.8). Always a download, never inline. */
export async function GET(request: NextRequest, { params }: { params: Promise<{ studentId: string; proofId: string }> }) {
  const { studentId, proofId } = await params;
  if (!GUID.test(studentId) || !GUID.test(proofId)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/portal/children/${studentId}/proofs/${proofId}/file`, { allowInline: false });
}
