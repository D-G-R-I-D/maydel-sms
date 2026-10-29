import type { NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, proxyImageDelete, proxyImageUpload } from "@/lib/media/image-proxy";

/** E19: a store item's photo (admin, bursar); the API returns the updated item. */
type Params = { params: Promise<{ id: string }> };
const path = (id: string) => (GUID.test(id) ? `/api/store/items/${id}/image` : null);

export async function PUT(request: NextRequest, { params }: Params) {
  const p = path((await params).id);
  return p ? proxyImageUpload(request, p, "photo") : bodyless(404);
}

export async function DELETE(request: NextRequest, { params }: Params) {
  const p = path((await params).id);
  return p ? proxyImageDelete(request, p) : bodyless(404);
}
