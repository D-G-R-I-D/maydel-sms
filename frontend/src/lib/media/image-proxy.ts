import "server-only";
import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/config";
import { toProblem } from "@/lib/api/problem";
import { crossSiteRejection, problemResponse, withSession, type ApiReply } from "@/lib/auth/bff";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import { MAX_PHOTO_BYTES } from "./photos";

/**
 * Shared BFF plumbing for private images (photos, school logo/signature; ADR-006). The browser never
 * calls the API. GET streams with the API's ETag/Cache-Control (If-None-Match passes through → 304);
 * PUT re-sends the multipart upload; DELETE removes. Bearer from the httpOnly cookie, ≤ 1 refresh.
 */

async function forward(request: NextRequest, path: string, init: { method: string; body?: ArrayBuffer; contentType?: string }) {
  const extra: Record<string, string> = {};
  const ifNoneMatch = request.headers.get("if-none-match");
  if (ifNoneMatch && init.method === "GET") extra["If-None-Match"] = ifNoneMatch;
  if (init.contentType) extra["Content-Type"] = init.contentType;

  return withSession<Response>(request, async (accessToken): Promise<ApiReply<Response>> => {
    let res: Response;
    try {
      res = await fetch(`${apiBaseUrl()}${path}`, {
        method: init.method,
        headers: { ...forwardingHeaders(request.headers), Authorization: `Bearer ${accessToken}`, ...extra },
        body: init.body,
        cache: "no-store",
      });
    } catch {
      return { ok: false, problem: { status: 503, title: "The school system is unavailable right now." } };
    }
    // Only a code-less 401 is worth a refresh-and-retry; everything else goes back as-is.
    if (res.status === 401) return { ok: false, problem: toProblem(401, await res.json().catch(() => null)) };
    return { ok: true, status: res.status, data: res };
  });
}

export function bodyless(status: number) {
  return new NextResponse(null, { status, headers: { "Cache-Control": "no-store" } });
}

/** Copies an image response (or 304) through, keeping its caching headers. */
export function passImage(res: Response): NextResponse {
  if (res.status === 304) {
    const headers = new Headers({ "Cache-Control": res.headers.get("cache-control") ?? "private, max-age=86400" });
    const etag = res.headers.get("etag");
    if (etag) headers.set("ETag", etag);
    return new NextResponse(null, { status: 304, headers });
  }
  if (!res.ok) return bodyless(res.status); // 403 / 404: same status, no body
  const headers = new Headers();
  for (const h of ["content-type", "etag", "cache-control", "content-length", "last-modified"]) {
    const v = res.headers.get(h);
    if (v) headers.set(h, v);
  }
  headers.set("X-Content-Type-Options", "nosniff");
  return new NextResponse(res.body, { status: 200, headers });
}

export async function proxyImageGet(request: NextRequest, path: string): Promise<NextResponse> {
  const { reply, finish } = await forward(request, path, { method: "GET" });
  if (!reply.ok) return finish(bodyless(reply.problem.status));
  return finish(passImage(reply.data));
}

export async function proxyImageUpload(request: NextRequest, path: string, noun = "photo", method: "PUT" | "POST" = "PUT"): Promise<NextResponse> {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const contentType = request.headers.get("content-type") ?? "";
  if (!contentType.startsWith("multipart/form-data")) {
    return problemResponse({ status: 400, title: `Upload a ${noun}.`, code: "validation", errors: { file: [`Upload a JPEG, PNG or WebP ${noun} (up to 12 MB).`] } });
  }
  const declared = Number(request.headers.get("content-length") ?? "0");
  if (declared > MAX_PHOTO_BYTES + 64 * 1024) {
    return problemResponse({ status: 400, title: `That ${noun} is too large.`, code: "validation", errors: { file: [`The ${noun} must be 12 MB or smaller.`] } });
  }
  const body = await request.arrayBuffer();
  const { reply, finish } = await forward(request, path, { method, body, contentType });
  if (!reply.ok) return finish(problemResponse(reply.problem));
  const res = reply.data;
  const json: unknown = await res.json().catch(() => null);
  if (!res.ok) return finish(problemResponse(toProblem(res.status, json)));
  return finish(NextResponse.json(json, { status: 200 }));
}

/** DELETE; passes a JSON body through when the API returns one (e.g. the updated profile), else 204. */
export async function proxyImageDelete(request: NextRequest, path: string): Promise<NextResponse> {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const { reply, finish } = await forward(request, path, { method: "DELETE" });
  if (!reply.ok) return finish(problemResponse(reply.problem));
  const res = reply.data;
  const json: unknown = res.status === 204 ? null : await res.json().catch(() => null);
  if (!res.ok) return finish(problemResponse(toProblem(res.status, json)));
  return finish(json === null ? bodyless(204) : NextResponse.json(json, { status: 200 }));
}
