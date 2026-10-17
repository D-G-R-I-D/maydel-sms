import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** A parent's/student's own invoice PDF (E6.3); the API checks the child belongs to them. */
export async function GET(request: NextRequest, { params }: { params: Promise<{ studentId: string; invoiceId: string }> }) {
  const { studentId, invoiceId } = await params;
  if (!GUID.test(studentId) || !GUID.test(invoiceId)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/portal/children/${studentId}/invoices/${invoiceId}/pdf`);
}
