import type { NextConfig } from "next";

// Share-a-link tunnels for `npm run dev` (VS Code port forwarding, Cloudflare quick tunnels, ngrok).
// `**` = any number of labels (abc-3000.uks1.devtunnels.ms); `*` would match only one.
const devTunnels = ["**.devtunnels.ms", "**.trycloudflare.com", "**.ngrok-free.app"];

const isDev = process.env.NODE_ENV !== "production";

/**
 * Hosts whose pages may call Server Actions besides our own. Next rejects an action whose Origin
 * host doesn't match X-Forwarded-Host (or Host), and tunnels break that both ways:
 * - some keep the browser's Origin (`https://abc-3000.uks1.devtunnels.ms`) but send Host `localhost:3000`;
 * - VS Code dev tunnels rewrite Origin to `http://localhost:3000` and put the tunnel in X-Forwarded-Host.
 * So in development we also trust the dev server's own localhost origin. Only a page served by this
 * dev server can send that Origin, so it isn't a CSRF hole. Mirrors the BFF's TRUSTED_ORIGINS
 * (lib/auth/origins.ts): in production, only what TRUSTED_ORIGINS lists.
 */
function serverActionOrigins(): string[] {
  const raw = process.env.TRUSTED_ORIGINS;
  const listed =
    raw === undefined
      ? isDev
        ? devTunnels
        : []
      : raw
          .split(",")
          .map((o) => o.trim().toLowerCase().replace(/^https?:\/\//, "").replace(/^\*\./, "**."))
          .filter(Boolean);
  const port = process.env.PORT ?? "3000";
  return isDev ? [...listed, `localhost:${port}`, `127.0.0.1:${port}`] : listed;
}

const nextConfig: NextConfig = {
  output: "standalone",
  // `npm run dev` only: let tunnel links load dev assets/HMR.
  allowedDevOrigins: devTunnels,
  experimental: {
    serverActions: { allowedOrigins: serverActionOrigins() },
  },
  poweredByHeader: false,
  images: {
    // Query-free local images only (Next 16 blocks query strings unless listed): brand files, website
    // photos, and CMS pictures through our proxy, versioned in the path.
    localPatterns: [
      { pathname: "/brand/**", search: "" },
      { pathname: "/site/**", search: "" },
      { pathname: "/api/site/images/**", search: "" },
    ],
  },
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
          // Location for this site only: staff clock-in can require it, and the admin sets the school's point.
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(self)" },
        ],
      },
    ];
  },
};

export default nextConfig;
