import "server-only";

/**
 * The address phones should open when they scan the kiosk QR, from PUBLIC_SITE_URL (e.g. a dev tunnel
 * or the real site). Null when unset or not an http(s) origin; the kiosk then uses its own address.
 */
export function publicSiteOrigin(): string | null {
  const raw = process.env.PUBLIC_SITE_URL?.trim();
  if (!raw) return null;
  try {
    const u = new URL(raw);
    return u.protocol === "https:" || u.protocol === "http:" ? u.origin : null;
  } catch {
    return null;
  }
}
