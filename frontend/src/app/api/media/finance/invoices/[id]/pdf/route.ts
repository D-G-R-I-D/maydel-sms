import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** Invoice PDF for staff (E10.3), proxied from the API. */
export async function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/finance/invoices/${id}/pdf`);
}
