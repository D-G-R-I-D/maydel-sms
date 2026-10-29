import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** E19: a shop order's receipt for the child's parents (404 for anyone else). */
type Params = { params: Promise<{ studentId: string; orderId: string }> };

export async function GET(request: NextRequest, { params }: Params) {
  const { studentId, orderId } = await params;
  if (!GUID.test(studentId) || !GUID.test(orderId)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/portal/children/${studentId}/shop/orders/${orderId}/receipt.pdf`, { allowInline: true });
}
