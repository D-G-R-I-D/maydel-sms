import type { NextRequest } from "next/server";
import { proxyImageUpload } from "@/lib/media/image-proxy";

/** A picture inside formatted content (admin): stored by the API and returned as { id, url }. */
export async function POST(request: NextRequest) {
  return proxyImageUpload(request, "/api/cms/media", "picture", "POST");
}
