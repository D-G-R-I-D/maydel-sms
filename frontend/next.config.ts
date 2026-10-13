import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  // `npm run dev` only: let VS Code port-forwarding links load dev assets/HMR. `**` = any number of labels (abc-3000.uks1.devtunnels.ms); `*` would match only one.
  allowedDevOrigins: ["**.devtunnels.ms", "**.trycloudflare.com", "**.ngrok-free.app"],
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
