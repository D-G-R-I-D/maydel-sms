import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";
import { isDate } from "@/lib/clock/clock";

/** Bus trips CSV (admin), proxied from the API so the browser never calls it directly. */
export async function GET(request: NextRequest) {
  const sp = request.nextUrl.searchParams;
  const from = sp.get("from") ?? undefined;
  const to = sp.get("to") ?? undefined;
  const busId = sp.get("busId");
  if (!isDate(from) || !isDate(to) || (busId && !GUID.test(busId))) return new NextResponse("Not found.", { status: 404 });
  const q = new URLSearchParams({ from, to });
  if (busId) q.set("busId", busId);
  return proxyBinaryGet(request, `/api/transport/trips.csv?${q.toString()}`);
}
