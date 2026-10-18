import type { NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, proxyImageDelete, proxyImageGet, proxyImageUpload } from "@/lib/media/image-proxy";

/** BFF proxy for a question's picture (E16.1, staff who can see the question); re-encoded to PNG by the API. */

type Params = { params: Promise<{ id: string }> };
const path = (id: string) => (GUID.test(id) ? `/api/cbt/questions/${id}/image` : null);

export async function GET(request: NextRequest, { params }: Params) {
  const p = path((await params).id);
  return p ? proxyImageGet(request, p) : bodyless(404);
}

export async function PUT(request: NextRequest, { params }: Params) {
  const p = path((await params).id);
  return p ? proxyImageUpload(request, p, "picture") : bodyless(404);
}

export async function DELETE(request: NextRequest, { params }: Params) {
  const p = path((await params).id);
  return p ? proxyImageDelete(request, p) : bodyless(404);
}
