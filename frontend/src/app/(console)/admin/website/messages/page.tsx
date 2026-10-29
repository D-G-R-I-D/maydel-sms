import type { Metadata } from "next";
import Link from "next/link";
import { Inbox, Phone } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatLagos } from "@/lib/time/lagos";
import { messageStatuses, parseStatus, statusTone } from "@/lib/website/cms";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { MessageActions } from "@/components/website/message-actions";

export const metadata: Metadata = { title: "Messages" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E7 console: messages from the website's contact form (also emailed to the school address). */
export default async function MessagesPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Messages" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  // Inbox by default: new ones first; "all" shows every status.
  const raw = one(sp.status);
  const status = raw === "all" ? undefined : (parseStatus(raw) ?? "New");
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const list = await load((api) => api.GET("/api/cms/messages", { params: { query: { ...(status ? { status } : {}), page, pageSize: 20 } } }));
  const href = (s: string, p = 1) => `/admin/website/messages?status=${s}${p > 1 ? `&page=${p}` : ""}`;
  const current = status ?? "all";

  return (
    <>
      <PageHeader title="Messages" description="From the contact form on the website. Each one is also emailed to the school address in Settings → School." />
      <nav aria-label="Message status" className="mb-5 inline-flex flex-wrap rounded-input bg-surface-2 p-1">
        {[...messageStatuses, "all" as const].map((s) => (
          <Link
            key={s}
            href={href(s)}
            aria-current={current === s ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", current === s ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
          >
            {s === "all" ? "All" : s}
          </Link>
        ))}
      </nav>
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : list.data.items.length === 0 ? (
        <Card>
          <EmptyState icon={Inbox} title={status === "New" ? "No new messages" : "Nothing here"}>
            {status === "New" ? "You're up to date. New messages from the website will show here." : "Try another status."}
          </EmptyState>
        </Card>
      ) : (
        <>
          <ul className="space-y-3">
            {list.data.items.map((m) => (
              <li key={m.id}>
                <Card className="space-y-3 p-5">
                  <div className="flex flex-wrap items-start gap-x-3 gap-y-1">
                    <div className="min-w-0 flex-1">
                      <p className="font-medium">
                        {m.name} <span className="font-normal text-muted">&lt;{m.email}&gt;</span>
                      </p>
                      {m.subject && <p className="font-display text-lg font-semibold">{m.subject}</p>}
                    </div>
                    <Badge tone={statusTone[m.status]}>{m.status}</Badge>
                    <span className="w-full text-xs text-muted sm:w-auto">{formatLagos(m.createdAt)}</span>
                  </div>
                  <p className="text-sm whitespace-pre-line">{m.message}</p>
                  {m.phone && (
                    <a href={`tel:${m.phone.replace(/[^\d+]/g, "")}`} className="inline-flex items-center gap-1.5 text-sm text-brand hover:underline">
                      <Phone className="size-3.5" aria-hidden />
                      {m.phone}
                    </a>
                  )}
                  <MessageActions message={m} />
                </Card>
              </li>
            ))}
          </ul>
          <div className="mt-4">
            <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} hrefFor={(p) => href(current, p)} noun="messages" />
          </div>
        </>
      )}
    </>
  );
}
