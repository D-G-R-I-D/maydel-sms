import type { Metadata } from "next";
import Link from "next/link";
import Image from "next/image";
import { Megaphone, Pin, Plus, Search } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { siteImage } from "@/lib/site/images";
import { formatLagos } from "@/lib/time/lagos";
import { audienceLabel } from "@/lib/website/posts";
import { Badge, Button, Card, Input, PageHeader, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";

export const metadata: Metadata = { title: "News & notices" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E7 console: every post (news and notices), drafts included, newest first. */
export default async function NewsListPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="News & notices" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const view = one(sp.view) === "drafts" ? "drafts" : one(sp.view) === "published" ? "published" : "all";
  const search = one(sp.q)?.trim().slice(0, 100);
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const list = await load((api) =>
    api.GET("/api/cms/posts", { params: { query: { ...(search ? { search } : {}), ...(view === "all" ? {} : { published: view === "published" }), page, pageSize: 20 } } }),
  );
  const href = (v: string, p = 1) => {
    const q = new URLSearchParams();
    if (v !== "all") q.set("view", v);
    if (search) q.set("q", search);
    if (p > 1) q.set("page", String(p));
    const s = q.toString();
    return `/admin/website/news${s ? `?${s}` : ""}`;
  };
  const add = (
    <Link href="/admin/website/news/new" className={buttonClasses("primary")}>
      <Plus className="size-4" aria-hidden />
      New post
    </Link>
  );

  return (
    <>
      <PageHeader title="News & notices" description="News for the website, and notices for parents, students or staff. A post stays a draft until you publish it." actions={add} />
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <nav aria-label="Post status" className="inline-flex rounded-input bg-surface-2 p-1">
          {(["all", "published", "drafts"] as const).map((v) => (
            <Link
              key={v}
              href={href(v)}
              aria-current={view === v ? "page" : undefined}
              className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", view === v ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
            >
              {v === "all" ? "All" : v === "published" ? "Published" : "Drafts"}
            </Link>
          ))}
        </nav>
        <form action="/admin/website/news" className="flex min-w-0 flex-1 gap-2 sm:max-w-sm">
          {view !== "all" && <input type="hidden" name="view" value={view} />}
          <label htmlFor="news-q" className="sr-only">
            Search posts
          </label>
          <Input id="news-q" name="q" defaultValue={search} placeholder="Search titles" className="w-full" />
          <Button type="submit" variant="secondary" aria-label="Search">
            <Search className="size-4" aria-hidden />
          </Button>
        </form>
      </div>
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : list.data.items.length === 0 ? (
        <Card>
          <EmptyState icon={Megaphone} title={search ? "No posts match" : "No posts yet"} action={search ? undefined : add}>
            {search ? "Try another word." : "Write the first news item or notice."}
          </EmptyState>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <ul className="divide-y divide-border">
            {list.data.items.map((p) => {
              const img = siteImage(p.imageUrl);
              return (
                <li key={p.id}>
                  <Link href={`/admin/website/news/${p.id}`} className="flex items-center gap-4 px-5 py-3 hover:bg-surface-2">
                    <span className="relative hidden size-16 shrink-0 overflow-hidden rounded-input bg-surface-2 sm:block">
                      {img ? <Image src={img} alt="" fill sizes="64px" className="object-cover" /> : <Megaphone className="m-5 size-6 text-muted" aria-hidden />}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="truncate font-medium">{p.title}</span>
                        {p.isPinned && <Pin className="size-3.5 text-brand" aria-label="Pinned" />}
                      </span>
                      <span className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted">
                        <span>{audienceLabel(p.audience)}</span>
                        {p.category && <span>· {p.category}</span>}
                        {p.inCarousel && <Badge>Carousel</Badge>}
                        {p.inTicker && <Badge>Ticker</Badge>}
                        {p.isFeatured && <Badge>Pop-up</Badge>}
                      </span>
                    </span>
                    <span className="shrink-0 text-right">
                      <Badge tone={p.publishedAt ? "success" : "neutral"}>{p.publishedAt ? "Published" : "Draft"}</Badge>
                      <span className="mt-1 block text-xs text-muted">{formatLagos(p.updatedAt ?? p.createdAt)}</span>
                    </span>
                  </Link>
                </li>
              );
            })}
          </ul>
          <div className="border-t border-border p-4">
            <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} hrefFor={(p) => href(view, p)} noun="posts" />
          </div>
        </Card>
      )}
    </>
  );
}
