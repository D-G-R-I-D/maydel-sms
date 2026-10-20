import type { MetadataRoute } from "next";
import { apiBaseUrl } from "@/lib/api/config";

/** E7: only the public website's pages (and published news); app routes are never listed. */
const base = () => (process.env.SITE_URL ?? "http://localhost:3000").replace(/\/+$/, "");
const pages = ["/", "/about", "/admissions-info", "/news", "/events", "/gallery", "/staff", "/contact", "/admissions/apply"];

export const revalidate = 3600;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const entries: MetadataRoute.Sitemap = pages.map((p) => ({ url: `${base()}${p}`, changeFrequency: p === "/" || p === "/news" ? "daily" : "monthly" }));
  try {
    const res = await fetch(`${apiBaseUrl()}/api/public/site/news?page=1&pageSize=100`, { next: { revalidate } });
    if (res.ok) {
      const data = (await res.json()) as { items?: { slug: string; publishedAt: string }[] };
      for (const n of data.items ?? []) entries.push({ url: `${base()}/news/${n.slug}`, lastModified: n.publishedAt, changeFrequency: "monthly" });
    }
  } catch {
    // The sitemap still lists the fixed pages if the API is unreachable.
  }
  return entries;
}
