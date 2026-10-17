import { NextResponse, type NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { proxyImageUpload } from "@/lib/media/image-proxy";

/** Upload a proof of bank payment for one of the parent's own invoices (E10.8): multipart, field `file` plus details. */
export async function POST(request: NextRequest, { params }: { params: Promise<{ studentId: string; invoiceId: string }> }) {
  const { studentId, invoiceId } = await params;
  if (!GUID.test(studentId) || !GUID.test(invoiceId)) return new NextResponse(null, { status: 404 });
  return proxyImageUpload(request, `/api/portal/children/${studentId}/invoices/${invoiceId}/proofs`, "proof of payment", "POST");
}
