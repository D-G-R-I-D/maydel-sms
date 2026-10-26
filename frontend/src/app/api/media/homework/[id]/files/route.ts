import type { NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, proxyMultipart } from "@/lib/media/image-proxy";

/** E17.1 staff: attach a file to homework (PDF, photo or Office document, ≤ 10 MB; the API checks the content). */
type Params = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, { params }: Params) {
  const { id } = await params;
  if (!GUID.test(id)) return bodyless(404);
  return proxyMultipart(request, `/api/homework/${id}/files`, {
    method: "POST",
    maxBytes: 10 * 1024 * 1024 + 64 * 1024,
    field: "file",
    noun: "file",
    tooLarge: "Each file must be 10 MB or smaller.",
    wrongType: "Attach a PDF, a photo, or a Word, Excel or PowerPoint file (up to 10 MB).",
  });
}
