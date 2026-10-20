"use server";

import { headers } from "next/headers";
import { apiBaseUrl } from "@/lib/api/config";
import { toProblem, unreachableProblem } from "@/lib/api/problem";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import type { Loaded } from "@/lib/api/server";

/** E7: the website contact form (anonymous, rate-limited by the API on the visitor's IP). */
export async function sendContactMessage(body: { name: string; email: string; phone: string | null; subject: string | null; message: string; website: string }): Promise<Loaded<true>> {
  try {
    const res = await fetch(`${apiBaseUrl()}/api/public/site/contact`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json", ...forwardingHeaders(await headers()) },
      body: JSON.stringify(body),
      cache: "no-store",
    });
    if (res.ok) return { ok: true, data: true };
    return { ok: false, problem: toProblem(res.status, await res.json().catch(() => null)) };
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
}
