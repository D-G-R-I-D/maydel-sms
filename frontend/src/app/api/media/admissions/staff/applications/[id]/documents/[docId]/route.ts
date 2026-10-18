import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** A family's uploaded document, for staff (E11.5). Untrusted: always a download, never inline. */
export async function GET(request: NextRequest, { params }: { params: Promise<{ id: string; docId: string }> }) {
  const { id, docId } = await params;
  if (!GUID.test(id) || !GUID.test(docId)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/admissions/applications/${id}/documents/${docId}/download`, { allowInline: false });
}
