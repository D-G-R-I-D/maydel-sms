import "server-only";
import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/config";
import { problemMessage, toProblem } from "@/lib/api/problem";
import { withSession, type ApiReply } from "@/lib/auth/bff";
import { forwardingHeaders } from "@/lib/auth/forwarding";

export const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Streams a binary API response (PDFs) to the browser: bearer from the httpOnly cookie, at most one
 * refresh, browser IP/UA forwarded. Content-Type/-Disposition pass through; never cached shared.
 * Errors come back as short plain text, since these URLs are usually opened in a tab.
 */
export async function proxyBinaryGet(request: NextRequest, apiPath: string): Promise<NextResponse> {
  const { reply, finish } = await withSession<Response>(request, async (accessToken): Promise<ApiReply<Response>> => {
    let res: Response;
    try {
      res = await fetch(`${apiBaseUrl()}${apiPath}`, {
        headers: { ...forwardingHeaders(request.headers), Authorization: `Bearer ${accessToken}` },
        cache: "no-store",
      });
    } catch {
      return { ok: false, problem: { status: 503, title: "The school system is unavailable right now. Please try again shortly." } };
    }
    if (res.status === 401) return { ok: false, problem: toProblem(401, await res.json().catch(() => null)) };
    return { ok: true, status: res.status, data: res };
  });

  const text = (status: number, message: string) =>
    finish(new NextResponse(message, { status, headers: { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "no-store" } }));

  if (!reply.ok) return text(reply.problem.status, reply.problem.status === 401 ? "Your session has ended. Sign in again, then retry." : problemMessage(reply.problem));
  const res = reply.data;
  if (!res.ok) return text(res.status, problemMessage(toProblem(res.status, await res.json().catch(() => null))));

  const headers = new Headers({ "Cache-Control": "private, no-store", "X-Content-Type-Options": "nosniff" });
  for (const h of ["content-type", "content-disposition", "content-length"]) {
    const v = res.headers.get(h);
    if (v) headers.set(h, v);
  }
  return finish(new NextResponse(res.body, { status: 200, headers }));
}
