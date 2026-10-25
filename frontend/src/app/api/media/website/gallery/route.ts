import type { NextRequest } from "next/server";
import { proxyImageUpload } from "@/lib/media/image-proxy";

/** A gallery photo (admin): multipart `file`, `title`, `caption?`, `category`, `order?`, `isFeatured?`. */
export async function POST(request: NextRequest) {
  return proxyImageUpload(request, "/api/cms/gallery", "photo", "POST");
}
