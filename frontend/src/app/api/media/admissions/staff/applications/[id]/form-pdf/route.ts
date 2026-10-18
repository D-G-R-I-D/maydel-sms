import { NextResponse, type NextRequest } from "next/server";
import { GUID, proxyBinaryGet } from "@/lib/media/binary-proxy";

/** An applicant's form PDF, for staff (E11.5). School-generated, so it may be viewed inline. */
export async function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) return new NextResponse("Not found.", { status: 404 });
  return proxyBinaryGet(request, `/api/admissions/applications/${id}/form-pdf`);
}
