import type { NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";
import { bodyless } from "@/lib/media/image-proxy";

/** E17.2: download a note's attachment (the API checks the reader may open this book). */
type Params = { params: Promise<{ studentId: string; entryId: string }> };

export async function GET(request: NextRequest, { params }: Params) {
  const { studentId, entryId } = await params;
  return GUID.test(studentId) && GUID.test(entryId) ? proxyBinaryGet(request, `/api/books/${studentId}/entries/${entryId}/file`) : bodyless(404);
}
