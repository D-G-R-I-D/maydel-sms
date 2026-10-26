import type { Metadata } from "next";
import Link from "next/link";
import { Mail, Plus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatLagos } from "@/lib/time/lagos";
import { newsletterAudienceLabel, statusTone } from "@/lib/website/newsletters";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { NewsletterTabs } from "@/components/website/newsletter-tabs";

export const metadata: Metadata = { title: "Newsletters" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E12.6: newsletters by email to parents, staff and website subscribers — drafts, scheduled and sent. */
export default async function NewslettersPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Newsletters" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const page = Math.max(1, Number(one((await searchParams).page)) || 1);
  const list = await load((api) => api.GET("/api/cms/newsletters", { params: { query: { page, pageSize: 20 } } }));
  const add = (
    <Link href="/admin/website/newsletters/new" className={buttonClasses("primary")}>
      <Plus className="size-4" aria-hidden />
      New newsletter
    </Link>
  );

  return (
    <>
      <PageHeader title="Newsletter" description="Email newsletters to parents, staff and website subscribers, in the school's branded layout." actions={add} />
      <NewsletterTabs active="newsletters" />
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : list.data.items.length === 0 ? (
        <Card>
          <EmptyState icon={Mail} title="No newsletters yet" action={add}>
            Start from a template: termly newsletter, event invitation or announcement.
          </EmptyState>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <ul className="divide-y divide-border">
            {list.data.items.map((n) => (
              <li key={n.id}>
                <Link href={`/admin/website/newsletters/${n.id}`} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-5 py-3 hover:bg-surface-2">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{n.title}</span>
                    <span className="text-xs text-muted">{newsletterAudienceLabel(n.audience)}</span>
                  </span>
                  <span className="text-right text-xs text-muted">
                    {n.status === "Sent" && n.sentAt
                      ? `Sent ${formatLagos(n.sentAt)} · ${n.recipients} people`
                      : n.status === "Scheduled" && n.scheduledFor
                        ? `For ${formatLagos(n.scheduledFor)}`
                        : `Edited ${formatLagos(n.updatedAt ?? n.createdAt)}`}
                  </span>
                  <Badge tone={statusTone[n.status]}>{n.status}</Badge>
                </Link>
              </li>
            ))}
          </ul>
          <div className="border-t border-border p-4">
            <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} hrefFor={(p) => `/admin/website/newsletters${p > 1 ? `?page=${p}` : ""}`} noun="newsletters" />
          </div>
        </Card>
      )}
    </>
  );
}
