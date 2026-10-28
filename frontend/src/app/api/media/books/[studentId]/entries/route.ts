import type { NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, proxyMultipart } from "@/lib/media/image-proxy";

/** E17.2: add a note (`body` and/or one `file`) to a student's communication book. */
type Params = { params: Promise<{ studentId: string }> };

export async function POST(request: NextRequest, { params }: Params) {
  const { studentId } = await params;
  if (!GUID.test(studentId)) return bodyless(404);
  return proxyMultipart(request, `/api/books/${studentId}/entries`, {
    method: "POST",
    maxBytes: 10 * 1024 * 1024 + 128 * 1024,
    field: "file",
    noun: "note",
    tooLarge: "The file must be 10 MB or smaller.",
    wrongType: "Attach a PDF, a photo, or a Word, Excel or PowerPoint file (up to 10 MB).",
  });
}
