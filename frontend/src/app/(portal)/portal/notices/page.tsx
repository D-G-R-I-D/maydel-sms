import type { Metadata } from "next";
import { ExternalLink, Megaphone, Pin } from "lucide-react";
import { load } from "@/lib/api/server";
import { formatLagos } from "@/lib/time/lagos";
import { safeLink } from "@/lib/notifications/bell";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { RichContent } from "@/components/website/rich-content";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "School notices" };

/** E12.1: the school's notices for this family, pinned first. The bell links here, one anchor per notice. */
export default async function NoticesPage() {
  const inbox = await load((api) => api.GET("/api/notices"));
  return (
    <>
      <PageHeader title="School notices" description="News and reminders from the school office." />
      {!inbox.ok ? (
        <Card>
          <ProblemState problem={inbox.problem} />
        </Card>
      ) : inbox.data.notices.length === 0 ? (
        <Card>
          <EmptyState icon={Megaphone} title="No notices right now">
            When the school posts a notice, it will show here and on the bell.
          </EmptyState>
        </Card>
      ) : (
        <ul className="space-y-4">
          {inbox.data.notices.map((n) => {
            const href = n.linkUrl && (safeLink(n.linkUrl) ?? (/^https:\/\//.test(n.linkUrl) ? n.linkUrl : null));
            return (
              <li key={n.id}>
                <Card id={`notice-${n.id}`} className="scroll-mt-24 space-y-2 p-5 target:ring-2 target:ring-brand">
                  <div className="flex flex-wrap items-center gap-2">
                    {n.isPinned && (
                      <Badge tone="brand">
                        <Pin className="mr-1 size-3" aria-hidden />
                        Pinned
                      </Badge>
                    )}
                    {n.category && <Badge>{n.category}</Badge>}
                    {!n.isRead && <Badge tone="warning">New</Badge>}
                    <span className="text-xs text-muted">{formatLagos(n.publishedAt)}</span>
                  </div>
                  <h2 className="font-display text-xl font-semibold">{n.title}</h2>
                  <RichContent html={n.bodyHtml} text={n.body || n.summary} className="text-sm" />
                  {href && (
                    <a href={href} className="inline-flex items-center gap-1 text-sm font-medium text-brand hover:underline" {...(href.startsWith("/") ? {} : { target: "_blank", rel: "noopener noreferrer" })}>
                      {n.linkText || "Read more"}
                      {!href.startsWith("/") && <ExternalLink className="size-3.5" aria-hidden />}
                    </a>
                  )}
                </Card>
              </li>
            );
          })}
        </ul>
      )}
    </>
  );
}
