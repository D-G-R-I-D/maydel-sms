import type { Metadata } from "next";
import Link from "next/link";
import { Download, MailCheck } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatLagos } from "@/lib/time/lagos";
import { Badge, Card, PageHeader, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { UnsubscribeButton } from "@/components/website/unsubscribe-button";
import { NewsletterTabs } from "@/components/website/newsletter-tabs";

export const metadata: Metadata = { title: "Newsletter subscribers" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E7 console: addresses from the website's newsletter sign-up, with CSV export. */
export default async function SubscribersPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Newsletter subscribers" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const view = one(sp.view) === "all" ? "all" : "active";
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const list = await load((api) => api.GET("/api/cms/subscribers", { params: { query: { ...(view === "active" ? { active: true } : {}), page, pageSize: 50 } } }));
  const href = (v: string, p = 1) => `/admin/website/subscribers?view=${v}${p > 1 ? `&page=${p}` : ""}`;

  return (
    <>
      <PageHeader
        title="Newsletter"
        description="People who signed up on the website. Newsletters go to confirmed, active addresses; the CSV lists those only."
        actions={
          <a href="/api/media/website/subscribers-csv" className={buttonClasses("secondary")} download>
            <Download className="size-4" aria-hidden />
            Export CSV
          </a>
        }
      />
      <NewsletterTabs active="subscribers" />
      <nav aria-label="Subscriber filter" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
        {(["active", "all"] as const).map((v) => (
          <Link
            key={v}
            href={href(v)}
            aria-current={view === v ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", view === v ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
          >
            {v === "active" ? "Active" : "All, including unsubscribed"}
          </Link>
        ))}
      </nav>
      <Card className="overflow-hidden">
        {!list.ok ? (
          <ProblemState problem={list.problem} />
        ) : list.data.items.length === 0 ? (
          <EmptyState icon={MailCheck} title="No subscribers yet">
            Visitors can sign up in the website footer.
          </EmptyState>
        ) : (
          <>
            <p className="border-b border-border px-5 py-3 text-sm text-muted">
              <span className="tabular">{list.data.totalCount}</span> {view === "active" ? "active" : "in total"}
            </p>
            <ul className="divide-y divide-border">
              {list.data.items.map((s) => (
                <li key={s.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-5 py-2.5 text-sm">
                  <span className="min-w-0 flex-1 truncate font-medium">{s.email}</span>
                  <span className="text-xs text-muted">Since {formatLagos(s.createdAt)}</span>
                  {s.isActive && !s.confirmed && <Badge tone="warning">Pending confirmation</Badge>}
                  {s.isActive ? <UnsubscribeButton subscriber={s} /> : <Badge>Unsubscribed</Badge>}
                </li>
              ))}
            </ul>
            <div className="border-t border-border p-4">
              <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} hrefFor={(p) => href(view, p)} noun="subscribers" />
            </div>
          </>
        )}
      </Card>
    </>
  );
}
