import "server-only";
import createClient from "openapi-fetch";
import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import type { paths } from "./schema";
import { apiBaseUrl } from "./config";
import { ProblemCodes, toProblem, unreachableProblem, type Problem } from "./problem";
import { ACCESS_COOKIE } from "@/lib/auth/cookies";
import { forwardingHeaders } from "@/lib/auth/forwarding";

/** Set by proxy.ts on every page request so server code knows where to send the user back to. */
export const PATH_HEADER = "x-maydel-path";

/**
 * Typed API client for Server Components, Server Actions and route handlers.
 * Adds the bearer from the httpOnly cookie and forwards the browser's IP and User-Agent.
 */
export async function serverApi() {
  const [jar, incoming] = await Promise.all([cookies(), headers()]);
  const token = jar.get(ACCESS_COOKIE)?.value;
  return createClient<paths>({
    baseUrl: apiBaseUrl(),
    headers: {
      Accept: "application/json",
      ...forwardingHeaders(incoming),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    fetch: (request: Request) => fetch(request, { cache: "no-store" }),
  });
}

export type Loaded<T> = { ok: true; data: T } | { ok: false; problem: Problem };

type FetchResult<T> = { data?: T; error?: unknown; response: Response };

/**
 * Runs an API call for a page and applies the session rules:
 * 401 → one refresh via /api/auth/refresh (which falls back to /login), 403 password-change → /change-password.
 * Everything else comes back as a Problem for the page's error / no-access state.
 */
export async function load<T>(call: (api: Awaited<ReturnType<typeof serverApi>>) => Promise<FetchResult<T>>): Promise<Loaded<T>> {
  const api = await serverApi();
  let result: FetchResult<T>;
  try {
    result = await call(api);
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
  const { data, error, response } = result;
  if (response.ok && data !== undefined) return { ok: true, data };

  const problem = toProblem(response.status, error);
  if (problem.status === 401) {
    const path = (await headers()).get(PATH_HEADER) ?? "/";
    redirect(`/api/auth/refresh?next=${encodeURIComponent(path)}`);
  }
  if (problem.status === 403 && problem.code === ProblemCodes.passwordChangeRequired) {
    redirect("/change-password");
  }
  return { ok: false, problem };
}
