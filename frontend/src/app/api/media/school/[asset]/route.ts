import { type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/config";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import { bodyless, passImage, proxyImageDelete, proxyImageGet, proxyImageUpload } from "@/lib/media/image-proxy";

/**
 * School logo and principal's signature (E2.6).
 * - GET logo: public (login page, portal), fetched without a token.
 * - GET signature: admin-only, with the user's bearer.
 * - PUT / DELETE either: admin, multipart field `file`; the API returns the updated profile.
 */

type Params = { params: Promise<{ asset: string }> };
const assets = new Set(["logo", "signature"]);

export async function GET(request: NextRequest, { params }: Params) {
  const { asset } = await params;
  if (asset === "signature") return proxyImageGet(request, "/api/school/profile/signature");
  if (asset !== "logo") return bodyless(404);
  try {
    const ifNoneMatch = request.headers.get("if-none-match");
    const res = await fetch(`${apiBaseUrl()}/api/public/school/logo`, {
      headers: { ...forwardingHeaders(request.headers), ...(ifNoneMatch ? { "If-None-Match": ifNoneMatch } : {}) },
      cache: "no-store",
    });
    return passImage(res);
  } catch {
    return bodyless(503);
  }
}

export async function PUT(request: NextRequest, { params }: Params) {
  const { asset } = await params;
  if (!assets.has(asset)) return bodyless(404);
  return proxyImageUpload(request, `/api/school/profile/${asset}`, asset === "logo" ? "logo" : "signature image");
}

export async function DELETE(request: NextRequest, { params }: Params) {
  const { asset } = await params;
  if (!assets.has(asset)) return bodyless(404);
  return proxyImageDelete(request, `/api/school/profile/${asset}`);
}
