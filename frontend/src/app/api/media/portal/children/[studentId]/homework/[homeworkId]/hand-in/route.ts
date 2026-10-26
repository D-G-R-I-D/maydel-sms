import type { NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { bodyless, proxyMultipart } from "@/lib/media/image-proxy";

/** E17.1 student: hand in `text` and up to 5 `files` (10 MB each). Handing in again replaces the last one. */
type Params = { params: Promise<{ studentId: string; homeworkId: string }> };

export async function POST(request: NextRequest, { params }: Params) {
  const { studentId, homeworkId } = await params;
  if (!GUID.test(studentId) || !GUID.test(homeworkId)) return bodyless(404);
  return proxyMultipart(request, `/api/portal/children/${studentId}/homework/${homeworkId}/hand-in`, {
    method: "POST",
    maxBytes: 5 * 10 * 1024 * 1024 + 256 * 1024,
    field: "files",
    noun: "hand-in",
    tooLarge: "Up to 5 files of 10 MB each.",
    wrongType: "Hand in text and/or files (PDF, photos, Word, Excel or PowerPoint).",
  });
}
