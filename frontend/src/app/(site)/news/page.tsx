import type { Metadata } from "next";
import Link from "next/link";
import { Newspaper } from "lucide-react";
import { loadSite } from "@/lib/site/api";
import { NewsCard } from "@/components/site/cards";
import { Pagination } from "@/components/ui/pagination";
import { Card, cn } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "News", description: "News and announcements from Maydel Spring Schools, Abuja." };

const PAGE_SIZE = 12;
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E7: published public news, pinned first, with an optional category filter. */
export default async function NewsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams;
  const category = one(sp.category)?.slice(0, 60);
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const [news, all] = await Promise.all([
    loadSite((api) => api.GET("/api/public/site/news", { params: { query: { category, page, pageSize: PAGE_SIZE } } })),
    loadSite((api) => api.GET("/api/public/site/news", { params: { query: { page: 1, pageSize: 50 } } })),
  ]);
  const categories = all.ok ? [...new Set(all.data.items.map((n) => n.category).filter((c): c is string => Boolean(c)))].sort() : [];
  const href = (p: number) => `/news?${new URLSearchParams({ ...(category ? { category } : {}), ...(p > 1 ? { page: String(p) } : {}) }).toString()}`;

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="font-display text-4xl font-semibold">News</h1>
      <p className="mt-1 text-muted">Announcements, achievements and life at Maydel.</p>
      {categories.length > 0 && (
        <nav aria-label="News categories" className="mt-5 flex flex-wrap gap-2">
          {[undefined, ...categories].map((c) => (
            <Link
              key={c ?? "all"}
              href={c ? `/news?category=${encodeURIComponent(c)}` : "/news"}
              aria-current={category === c ? "page" : undefined}
              className={cn("inline-flex min-h-9 items-center rounded-full border px-4 text-sm", category === c ? "border-brand bg-brand/10 font-medium text-brand" : "border-border hover:bg-surface-2")}
            >
              {c ?? "All"}
            </Link>
          ))}
        </nav>
      )}
      <div className="mt-6">
        {!news.ok ? (
          <Card>
            <ProblemState problem={news.problem} />
          </Card>
        ) : news.data.items.length === 0 ? (
          <Card>
            <EmptyState icon={Newspaper} title="No news yet">
              Check back soon.
            </EmptyState>
          </Card>
        ) : (
          <>
            <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {news.data.items.map((n) => (
                <li key={n.id}>
                  <NewsCard item={n} />
                </li>
              ))}
            </ul>
            {news.data.totalCount > PAGE_SIZE && (
              <Card className="mt-6 overflow-hidden">
                <Pagination page={page} pageSize={PAGE_SIZE} totalCount={news.data.totalCount} hrefFor={href} noun="articles" />
              </Card>
            )}
          </>
        )}
      </div>
    </div>
  );
}
