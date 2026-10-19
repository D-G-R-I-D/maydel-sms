import type { NextRequest } from "next/server";
import { proxyMultipart } from "@/lib/media/image-proxy";

/** BFF for starting an AI generation (E16.8): the request plus up to 5 files. The AI can take 30–90 s. */
export const maxDuration = 180;

export async function POST(request: NextRequest) {
  return proxyMultipart(request, "/api/cbt/ai/generations", {
    method: "POST",
    maxBytes: 5 * 12 * 1024 * 1024 + 256 * 1024,
    field: "files",
    noun: "set of files",
    tooLarge: "Upload up to 5 files: PDFs up to 5 MB, photos up to 12 MB.",
    wrongType: "Upload PDFs or photos of your notes.",
  });
}
