import type { NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, proxyImageDelete, proxyImageUpload } from "@/lib/media/image-proxy";

/** A post's cover picture (admin); the API returns the updated post. */
type Params = { params: Promise<{ id: string }> };
const path = (id: string) => (GUID.test(id) ? `/api/cms/posts/${id}/image` : null);

export async function PUT(request: NextRequest, { params }: Params) {
  const p = path((await params).id);
  return p ? proxyImageUpload(request, p, "picture") : bodyless(404);
}

export async function DELETE(request: NextRequest, { params }: Params) {
  const p = path((await params).id);
  return p ? proxyImageDelete(request, p) : bodyless(404);
}
