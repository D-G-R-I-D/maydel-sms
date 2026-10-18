import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** A parent's uploaded proof of payment, for staff (E10.8). Untrusted: always a download, never inline. */
export async function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/finance/proofs/${id}/file`, { allowInline: false });
}
