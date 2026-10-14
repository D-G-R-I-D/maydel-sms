import type { NextRequest } from "next/server";
import { bodyless, proxyImageDelete, proxyImageGet, proxyImageUpload } from "@/lib/media/image-proxy";
import { photoApiPath } from "@/lib/media/photos";

/** BFF proxy for private student/staff/portal photos (ADR-006); shared logic in lib/media/image-proxy. */

type Params = { params: Promise<{ kind: string; id: string }> };

export async function GET(request: NextRequest, { params }: Params) {
  const { kind, id } = await params;
  const path = photoApiPath(kind, id);
  return path ? proxyImageGet(request, path) : bodyless(404);
}

export async function PUT(request: NextRequest, { params }: Params) {
  const { kind, id } = await params;
  const path = kind === "portal" ? null : photoApiPath(kind, id); // portal photos are read-only
  return path ? proxyImageUpload(request, path, "photo") : bodyless(404);
}

export async function DELETE(request: NextRequest, { params }: Params) {
  const { kind, id } = await params;
  const path = kind === "portal" ? null : photoApiPath(kind, id);
  return path ? proxyImageDelete(request, path) : bodyless(404);
}
