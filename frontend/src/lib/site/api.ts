import "server-only";
import createClient from "openapi-fetch";
import type { components, paths } from "@/lib/api/schema";
import { apiBaseUrl } from "@/lib/api/config";
import { toProblem, unreachableProblem, type Problem } from "@/lib/api/problem";
import type { Loaded } from "@/lib/api/server";

/**
 * E7: the public website's API client. Anonymous on purpose (no cookies, no bearer), so site pages
 * render the same for everyone and can be cached; content is revalidated every minute.
 */
export const SITE_REVALIDATE = 60;

function publicApi() {
  return createClient<paths>({
    baseUrl: apiBaseUrl(),
    headers: { Accept: "application/json" },
    fetch: (request: Request) => fetch(request, { next: { revalidate: SITE_REVALIDATE } }),
  });
}

type FetchResult<T> = { data?: T; error?: unknown; response: Response };

export async function loadSite<T>(call: (api: ReturnType<typeof publicApi>) => Promise<FetchResult<T>>): Promise<Loaded<T>> {
  let result: FetchResult<T>;
  try {
    result = await call(publicApi());
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
  if (result.response.ok && result.data !== undefined) return { ok: true, data: result.data };
  const problem: Problem = toProblem(result.response.status, result.error);
  return { ok: false, problem };
}

export { siteImage } from "./images";

export type NewsSummary = components["schemas"]["NewsSummary"];
export type NewsArticle = components["schemas"]["NewsArticle"];
export type PublicNotice = components["schemas"]["PublicNotice"];
export type PublicEvent = components["schemas"]["PublicEvent"];
export type PublicPhoto = components["schemas"]["PublicPhoto"];
export type PublicStaff = components["schemas"]["PublicStaff"];
export type Slide = components["schemas"]["Slide"];
export type TickerItem = components["schemas"]["TickerItem"];

/** Settings → School, as the public site shows it (GET /api/public/school/profile). */
export type SchoolProfile = components["schemas"]["PublicSchoolProfile"];

const fallbackProfile: SchoolProfile = {
  name: "Maydel Spring Schools",
  motto: null,
  address: "",
  phone: null,
  email: null,
  website: null,
  whatsApp: null,
  whatsAppUrl: null,
  mapUrl: null,
  facebookUrl: null,
  instagramUrl: null,
  officeHours: null,
  logoUrl: null,
};

/** The school's public profile; just the name if the API can't be reached, so the site still renders. */
export async function loadSchoolProfile(): Promise<SchoolProfile> {
  const r = await loadSite((api) => api.GET("/api/public/school/profile"));
  return r.ok ? r.data : fallbackProfile;
}

/** "tel:" target from a display number. */
export const telHref = (phone: string) => `tel:${phone.replace(/[^0-9+]/g, "")}`;
