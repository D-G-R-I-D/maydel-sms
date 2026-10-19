import type { NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, proxyImageGet } from "@/lib/media/image-proxy";

/** BFF proxy for a question picture inside a student's own attempt (E16.3). */

type Params = { params: Promise<{ id: string; itemId: string }> };

export async function GET(request: NextRequest, { params }: Params) {
  const { id, itemId } = await params;
  return GUID.test(id) && GUID.test(itemId) ? proxyImageGet(request, `/api/student/attempts/${id}/items/${itemId}/image`) : bodyless(404);
}
