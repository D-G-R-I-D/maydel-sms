/**
 * Extra browser origins the BFF accepts besides its own host, for when a tunnel or proxy hides the
 * public hostname from the server (e.g. VS Code port forwarding shows `localhost:3000` as Host while
 * the browser sends `Origin: https://abc-3000.euw.devtunnels.ms`).
 *
 * `TRUSTED_ORIGINS` is a comma-separated list of origins; a leading `*.` in the host matches any
 * subdomain, so a tunnel link that changes every day keeps working. In development it defaults to
 * VS Code dev tunnels; in production it defaults to nothing (the real domain matches Host already).
 */
// Share-a-link tunnels for `npm run dev`: VS Code port forwarding, Cloudflare quick tunnels, ngrok.
const DEV_DEFAULT = "https://*.devtunnels.ms,https://*.trycloudflare.com,https://*.ngrok-free.app";

export function trustedOrigins(env: Record<string, string | undefined> = process.env): string[] {
  const raw = env.TRUSTED_ORIGINS ?? (env.NODE_ENV === "production" ? "" : DEV_DEFAULT);
  return raw.split(",").map((o) => o.trim().toLowerCase()).filter(Boolean);
}

export function isTrustedOrigin(origin: string, patterns: readonly string[] = trustedOrigins()): boolean {
  let url: URL;
  try {
    url = new URL(origin);
  } catch {
    return false;
  }
  const host = url.host.toLowerCase();
  return patterns.some((pattern) => {
    const [scheme, rest] = pattern.split("://");
    if (!rest || `${scheme}:` !== url.protocol) return false;
    if (!rest.startsWith("*.")) return rest === host;
    const suffix = rest.slice(1); // ".devtunnels.ms"
    return host.endsWith(suffix) && host.length > suffix.length;
  });
}
