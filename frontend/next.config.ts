import type { NextConfig } from "next";

// Share-a-link tunnels for `npm run dev` (VS Code port forwarding, Cloudflare quick tunnels, ngrok).
// `**` = any number of labels (abc-3000.uks1.devtunnels.ms); `*` would match only one.
const devTunnels = ["**.devtunnels.ms", "**.trycloudflare.com", "**.ngrok-free.app"];

/**
 * Hosts whose pages may call Server Actions besides our own. Next rejects an action whose Origin
 * doesn't match Host / X-Forwarded-Host, and tunnels show `localhost:3000` as Host, so without this
 * every save through a shared link fails. Mirrors the BFF's TRUSTED_ORIGINS (lib/auth/origins.ts):
 * dev tunnels by default in development, only what TRUSTED_ORIGINS lists in production.
 */
function serverActionOrigins(): string[] {
  const raw = process.env.TRUSTED_ORIGINS;
  if (raw === undefined) return process.env.NODE_ENV === "production" ? [] : devTunnels;
  return raw
    .split(",")
    .map((o) => o.trim().toLowerCase().replace(/^https?:\/\//, "").replace(/^\*\./, "**."))
    .filter(Boolean);
}

const nextConfig: NextConfig = {
  output: "standalone",
  // `npm run dev` only: let tunnel links load dev assets/HMR.
  allowedDevOrigins: devTunnels,
  experimental: {
    serverActions: { allowedOrigins: serverActionOrigins() },
  },
  poweredByHeader: false,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        ],
      },
    ];
  },
};

export default nextConfig;
