import { NextResponse, type NextRequest } from "next/server";
import { GUID } from "@/lib/media/binary-proxy";
import { proxyImageUpload } from "@/lib/media/image-proxy";

/** E11.5: upload a document to the family's own application (multipart: `kind` + `file`). */
export async function POST(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) return new NextResponse(null, { status: 404 });
  return proxyImageUpload(request, `/api/admissions/applications/${id}/documents`, "document", "POST");
}
