import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** E19: an order's A5 receipt (admin, bursar). */
type Params = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, { params }: Params) {
  const { id } = await params;
  if (!GUID.test(id)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/store/orders/${id}/receipt.pdf`, { allowInline: true });
}
