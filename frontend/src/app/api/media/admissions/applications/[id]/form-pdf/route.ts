import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** The family's filled-in application form PDF (E11.5); works as a draft preview too. School-generated. */
export async function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/admissions/applications/${id}/form.pdf`);
}
