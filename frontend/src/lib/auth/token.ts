/** Refresh when the access token has less than this left (handoff BFF rule 3). */
export const REFRESH_LEEWAY_MS = 2 * 60 * 1000;

/** Reads `exp` from a JWT without verifying it. Only used to decide when to refresh; the API verifies. */
export function jwtExpiry(token: string): number | null {
  const parts = token.split(".");
  if (parts.length !== 3) return null;
  try {
    const json = atob(parts[1].replace(/-/g, "+").replace(/_/g, "/"));
    const payload: unknown = JSON.parse(json);
    if (typeof payload === "object" && payload !== null && "exp" in payload && typeof payload.exp === "number") {
      return payload.exp * 1000;
    }
    return null;
  } catch {
    return null;
  }
}

export function needsRefresh(accessToken: string | undefined, now = Date.now()): boolean {
  if (!accessToken) return true;
  const exp = jwtExpiry(accessToken);
  return exp === null || exp - now < REFRESH_LEEWAY_MS;
}
