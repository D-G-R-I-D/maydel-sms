import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/config";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, passImage } from "@/lib/media/image-proxy";

/**
 * E7: public website pictures (news, gallery, staff), passed through with the API's public cache headers.
 * The version sits in the path (not ?v=) so next/image can optimise them with a query-free localPattern.
 */

const KINDS = new Set(["news", "gallery", "staff"]);
type Params = { params: Promise<{ kind: string; id: string; v: string }> };

export async function GET(request: NextRequest, { params }: Params): Promise<NextResponse> {
  const { kind, id, v } = await params;
  if (!KINDS.has(kind) || !GUID.test(id) || !/^[\w.-]{1,64}$/.test(v)) return bodyless(404);
  const headers: Record<string, string> = {};
  const etag = request.headers.get("if-none-match");
  if (etag) headers["If-None-Match"] = etag;
  try {
    const res = await fetch(`${apiBaseUrl()}/api/public/site/images/${kind}/${id}?v=${encodeURIComponent(v)}`, { headers, cache: "no-store" });
    return passImage(res);
  } catch {
    return bodyless(503);
  }
}
