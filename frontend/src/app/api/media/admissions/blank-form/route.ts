import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/config";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import { GUID } from "@/lib/media/binary-proxy";

/** E11.5: the blank admission form PDF (public, school-generated), proxied so the browser never calls the API. */
export async function GET(request: NextRequest) {
  const sessionId = request.nextUrl.searchParams.get("sessionId") ?? "";
  if (sessionId && !GUID.test(sessionId)) return new NextResponse("Not found.", { status: 404 });
  const q = sessionId ? `?sessionId=${sessionId}` : "";
  let res: Response;
  try {
    res = await fetch(`${apiBaseUrl()}/api/public/admissions/blank-form.pdf${q}`, { headers: forwardingHeaders(request.headers), cache: "no-store" });
  } catch {
    return new NextResponse("The school system is unavailable right now. Please try again shortly.", { status: 503 });
  }
  if (!res.ok) return new NextResponse("The blank form isn't available right now.", { status: res.status });
  const headers = new Headers({ "Cache-Control": "public, max-age=300", "X-Content-Type-Options": "nosniff" });
  for (const h of ["content-type", "content-disposition", "content-length"]) {
    const v = res.headers.get(h);
    if (v) headers.set(h, v);
  }
  return new NextResponse(res.body, { status: 200, headers });
}
