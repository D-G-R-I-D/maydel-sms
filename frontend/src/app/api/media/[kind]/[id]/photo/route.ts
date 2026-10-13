import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/config";
import { toProblem } from "@/lib/api/problem";
import { crossSiteRejection, problemResponse, withSession, type ApiReply } from "@/lib/auth/bff";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import { MAX_PHOTO_BYTES, photoApiPath } from "@/lib/media/photos";

/**
 * BFF proxy for private photos (ADR-006): the browser never calls the API. GET streams the image
 * with the API's ETag/Cache-Control (If-None-Match passes through, so 304s work); PUT re-sends the
 * multipart upload; DELETE removes. Bearer comes from the httpOnly cookie, with at most one refresh.
 */

type Params = { params: Promise<{ kind: string; id: string }> };

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

function bodyless(status: number) {
  return new NextResponse(null, { status, headers: { "Cache-Control": "no-store" } });
}

export async function GET(request: NextRequest, { params }: Params) {
  const path = photoApiPath((await params).kind, (await params).id);
  if (!path) return bodyless(404);
  const { reply, finish } = await forward(request, path, { method: "GET" });
  if (!reply.ok) return finish(bodyless(reply.problem.status));

  const res = reply.data;
  if (res.status === 304) {
    const headers = new Headers({ "Cache-Control": res.headers.get("cache-control") ?? "private, max-age=86400" });
    const etag = res.headers.get("etag");
    if (etag) headers.set("ETag", etag);
    return finish(new NextResponse(null, { status: 304, headers }));
  }
  if (!res.ok) return finish(bodyless(res.status)); // 403 / 404: same status, no body

  const headers = new Headers();
  for (const h of ["content-type", "etag", "cache-control", "content-length", "last-modified"]) {
    const v = res.headers.get(h);
    if (v) headers.set(h, v);
  }
  headers.set("X-Content-Type-Options", "nosniff");
  return finish(new NextResponse(res.body, { status: 200, headers }));
}

export async function PUT(request: NextRequest, { params }: Params) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const { kind, id } = await params;
  const path = photoApiPath(kind, id);
  if (!path) return bodyless(404);

  const contentType = request.headers.get("content-type") ?? "";
  if (!contentType.startsWith("multipart/form-data")) {
    return problemResponse({ status: 400, title: "Upload a photo.", code: "validation", errors: { file: ["Upload a JPEG, PNG or WebP photo (up to 12 MB)."] } });
  }
  const declared = Number(request.headers.get("content-length") ?? "0");
  if (declared > MAX_PHOTO_BYTES + 64 * 1024) {
    return problemResponse({ status: 400, title: "That photo is too large.", code: "validation", errors: { file: ["The photo must be 12 MB or smaller."] } });
  }

  const body = await request.arrayBuffer();
  const { reply, finish } = await forward(request, path, { method: "PUT", body, contentType });
  if (!reply.ok) return finish(problemResponse(reply.problem));
  const res = reply.data;
  const json: unknown = await res.json().catch(() => null);
  if (!res.ok) return finish(problemResponse(toProblem(res.status, json)));
  return finish(NextResponse.json(json, { status: 200 }));
}

export async function DELETE(request: NextRequest, { params }: Params) {
  const blocked = crossSiteRejection(request);
  if (blocked) return blocked;
  const { kind, id } = await params;
  const path = photoApiPath(kind, id);
  if (!path) return bodyless(404);

  const { reply, finish } = await forward(request, path, { method: "DELETE" });
  if (!reply.ok) return finish(problemResponse(reply.problem));
  const res = reply.data;
  if (!res.ok) return finish(problemResponse(toProblem(res.status, await res.json().catch(() => null))));
  return finish(bodyless(204));
}
