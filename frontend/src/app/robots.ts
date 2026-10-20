import type { MetadataRoute } from "next";

/** E7: crawl the public website; keep the app (console, portal, admissions, exam, sign-in) and the BFF out. */
export default function robots(): MetadataRoute.Robots {
  const base = (process.env.SITE_URL ?? "http://localhost:3000").replace(/\/+$/, "");
  return {
    rules: [{ userAgent: "*", allow: "/", disallow: ["/admin", "/portal", "/admissions/", "/exam", "/login", "/change-password", "/api/"] }],
    sitemap: `${base}/sitemap.xml`,
  };
}
