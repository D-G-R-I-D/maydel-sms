"use server";

import { headers } from "next/headers";
import createClient from "openapi-fetch";
import type { paths } from "@/lib/api/schema";
import { apiBaseUrl } from "@/lib/api/config";
import { toProblem, unreachableProblem } from "@/lib/api/problem";
import type { Loaded } from "@/lib/api/server";
import { forwardingHeaders } from "@/lib/auth/forwarding";

/**
 * E12.6 public newsletter actions: sign up, confirm, unsubscribe. Anonymous (no cookies, no bearer) and
 * never cached; the visitor's IP is forwarded so the API's rate limit applies per person.
 */
async function anon() {
  return createClient<paths>({
    baseUrl: apiBaseUrl(),
    headers: { Accept: "application/json", ...forwardingHeaders(await headers()) },
    fetch: (request: Request) => fetch(request, { cache: "no-store" }),
  });
}

async function run(call: (api: Awaited<ReturnType<typeof anon>>) => Promise<{ response: Response; error?: unknown }>): Promise<Loaded<true>> {
  try {
    const { response, error } = await call(await anon());
    return response.ok ? { ok: true, data: true } : { ok: false, problem: toProblem(response.status, error) };
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
}

/** Footer sign-up. `website` is the honeypot: real visitors leave it empty. A confirmation email follows. */
export async function subscribe(email: string, website: string): Promise<Loaded<true>> {
  return run((api) => api.POST("/api/public/site/subscribe", { body: { email: email.trim(), website: website || null } }));
}

const token = (t: string) => t.length >= 8 && t.length <= 1024 && !/\s/.test(t);

export async function confirmSubscription(t: string): Promise<Loaded<true>> {
  if (!token(t)) return { ok: false, problem: toProblem(404, { code: "newsletter.link", detail: "This link isn't valid." }) };
  return run((api) => api.POST("/api/public/newsletter/confirm", { params: { query: { t } } }));
}

export async function unsubscribeNewsletter(t: string): Promise<Loaded<true>> {
  if (!token(t)) return { ok: false, problem: toProblem(404, { code: "newsletter.link", detail: "This link isn't valid." }) };
  return run((api) => api.POST("/api/public/newsletter/unsubscribe", { params: { query: { t } } }));
}
