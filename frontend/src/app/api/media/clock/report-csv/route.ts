import { NextResponse, type NextRequest } from "next/server";
import { proxyBinaryGet } from "@/lib/media/binary-proxy";
import { isDate } from "@/lib/clock/clock";

/** Staff attendance report CSV (admin), proxied from the API so the browser never calls it directly. */
export async function GET(request: NextRequest) {
  const sp = request.nextUrl.searchParams;
  const from = sp.get("from") ?? undefined;
  const to = sp.get("to") ?? undefined;
  if (!isDate(from) || !isDate(to)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/clock/report.csv?${new URLSearchParams({ from, to }).toString()}`);
}
