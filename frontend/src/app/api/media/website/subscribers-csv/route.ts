import type { NextRequest } from "next/server";
import { proxyBinaryGet } from "@/lib/media/binary-proxy";

/** Newsletter subscribers CSV (admin), proxied from the API so the browser never calls it directly. */
export async function GET(request: NextRequest) {
  return proxyBinaryGet(request, "/api/cms/subscribers.csv");
}
