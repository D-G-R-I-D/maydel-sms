type HeaderSource = { get(name: string): string | null };

/**
 * The browser's IP as this server sees it. Uses the right-most X-Forwarded-For hop: a reverse
 * proxy in front of the web app appends the real client address, while anything to its left is
 * client-supplied. Deploy behind a proxy that sets/appends X-Forwarded-For.
 */
export function clientIp(headers: HeaderSource): string | undefined {
  const xff = headers.get("x-forwarded-for");
  if (xff) {
    const hops = xff.split(",").map((h) => h.trim()).filter(Boolean);
    const last = hops.at(-1);
    if (last) return stripIpv4Mapped(last);
  }
  const real = headers.get("x-real-ip")?.trim();
  return real ? stripIpv4Mapped(real) : undefined;
}

function stripIpv4Mapped(ip: string): string {
  return ip.startsWith("::ffff:") ? ip.slice(7) : ip;
}

/** Headers to send on every API call so the API rate-limits and audits the real browser. */
export function forwardingHeaders(headers: HeaderSource): Record<string, string> {
  const out: Record<string, string> = {};
  const ip = clientIp(headers);
  if (ip) out["X-Forwarded-For"] = ip;
  const ua = headers.get("user-agent");
  if (ua) out["User-Agent"] = ua;
  return out;
}
