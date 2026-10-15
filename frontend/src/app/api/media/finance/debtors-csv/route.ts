import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** Debtors CSV (E10 part 2a), proxied from the API so the browser never calls it directly. */
export async function GET(request: NextRequest) {
  const sp = request.nextUrl.searchParams;
  const termId = sp.get("termId") ?? "";
  const classRoomId = sp.get("classRoomId");
  if (!GUID.test(termId) || (classRoomId && !GUID.test(classRoomId))) return new NextResponse("Not found.", { status: 404 });
  const q = new URLSearchParams({ termId });
  if (classRoomId) q.set("classRoomId", classRoomId);
  if (sp.get("overdueOnly") === "true") q.set("overdueOnly", "true");
  return proxyBinaryGet(request, `/api/finance/debtors.csv?${q.toString()}`);
}
