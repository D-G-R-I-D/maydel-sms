import type { NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";
import { bodyless } from "@/lib/media/image-proxy";

/** E17.1: download a file from a hand-in (the student, their guardians, or their teachers). */
type Params = { params: Promise<{ id: string; submissionId: string; fileId: string }> };

export async function GET(request: NextRequest, { params }: Params) {
  const { id, submissionId, fileId } = await params;
  return [id, submissionId, fileId].every((x) => GUID.test(x)) ? proxyBinaryGet(request, `/api/homework/${id}/hand-ins/${submissionId}/files/${fileId}`) : bodyless(404);
}
