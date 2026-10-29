import type { NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";
import { bodyless } from "@/lib/media/image-proxy";

/** E17.1: download a homework attachment (the API checks the reader can see that homework). */
type Params = { params: Promise<{ id: string; fileId: string }> };

export async function GET(request: NextRequest, { params }: Params) {
  const { id, fileId } = await params;
  return GUID.test(id) && GUID.test(fileId) ? proxyBinaryGet(request, `/api/homework/${id}/files/${fileId}`) : bodyless(404);
}
