import { isTrustedOrigin } from "./origins";

export type SameSiteCheck = { ok: true } | { ok: false; origin: string; hosts: string[] };

/**
 * Same-site means the Origin's host equals a host this request was addressed to: the `Host` header *or* any
 * `X-Forwarded-Host` hop. Tunnels and proxies differ in which one they rewrite (VS Code dev tunnels rewrite Origin
 * and Host to localhost but keep the public host in X-Forwarded-Host), and a cross-site attacker's browser can set
 * neither, so accepting either is safe. TRUSTED_ORIGINS covers proxies that hide the public host entirely.
 * Requests without an Origin (same-origin GETs, server-to-server calls) pass; SameSite=Lax cookies cover those.
 */
export function checkSameSite(headers: Headers): SameSiteCheck {
  const origin = headers.get("origin");
  if (!origin) return { ok: true };

  const hosts = requestHosts(headers);
  let originHost: string | null = null;
  try {
    originHost = new URL(origin).host.toLowerCase();
  } catch {
    // malformed or opaque ("null") Origin: never same-site
  }
  if (originHost && hosts.includes(originHost)) return { ok: true };
  if (isTrustedOrigin(origin)) return { ok: true };
  return { ok: false, origin, hosts };
}

/** Every host the request was addressed to: Host plus each X-Forwarded-Host hop, lower-cased. */
export function requestHosts(headers: Headers): string[] {
  const forwarded = (headers.get("x-forwarded-host") ?? "").split(",");
  return [headers.get("host") ?? "", ...forwarded].map((h) => h.trim().toLowerCase()).filter(Boolean);
}
