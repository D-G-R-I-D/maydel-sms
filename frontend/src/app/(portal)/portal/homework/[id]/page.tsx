import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Download, MessageSquare } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { isStudent } from "@/lib/auth/roles";
import { todayWat } from "@/lib/clock/clock";
import { GUID } from "@/lib/media/binary-proxy";
import { formatLagos } from "@/lib/time/lagos";
import { dueLabel, fileSize, stateLabel, stateTone, type MyHomework } from "@/lib/homework/homework";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { HandInForm } from "@/components/homework/hand-in-form";
import { RichContent } from "@/components/website/rich-content";

export const metadata: Metadata = { title: "Homework" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/**
 * E17.1 portal: one piece of homework. Students hand in here; parents see it read-only. Notifications
 * link here without a child, so with several children we find the one this homework belongs to.
 */
export default async function PortalHomeworkDetail({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const { id } = await params;
  if (!GUID.test(id)) notFound();
  const requested = one((await searchParams).child);
  const children = await load((api) => api.GET("/api/portal/children"));
  if (!children.ok || children.data.length === 0) notFound();
  const order = [...children.data].sort((a, b) => (a.studentId === requested ? -1 : b.studentId === requested ? 1 : 0));
  let found: { child: (typeof order)[number]; m: MyHomework } | null = null;
  for (const c of order) {
    const r = await load((api) => api.GET("/api/portal/children/{studentId}/homework/{homeworkId}", { params: { path: { studentId: c.studentId, homeworkId: id } } }));
    if (r.ok) {
      found = { child: c, m: r.data };
      break;
    }
  }
  if (!found) notFound();
  const { child, m } = found;
  const h = m.homework;
  const today = todayWat();
  const canHandIn = isStudent(user.roles) && h.acceptsSubmissions && m.state !== "Seen";

  return (
    <>
      <Link href={`/portal/homework?child=${child.studentId}`} className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        Homework
      </Link>
      <PageHeader title={h.title} description={`${h.subject ?? "General"} · ${h.classRoom} · set by ${h.setBy}${children.data.length > 1 ? ` · ${child.name}` : ""}`} />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_18rem] lg:items-start">
        <div className="space-y-5">
          <Card className="space-y-4 p-5">
            <p className={cn("text-sm font-medium", m.isOverdue ? "text-danger" : "text-brand")}>{m.isOverdue ? `Overdue · ${dueLabel(h.dueOn, today)}` : dueLabel(h.dueOn, today)}</p>
            <RichContent html={h.instructionsHtml} text={h.instructions || "No extra instructions."} />
            {h.files.length > 0 && (
              <ul className="space-y-1 border-t border-border pt-3 text-sm">
                {h.files.map((f) => (
                  <li key={f.id}>
                    <a href={`/api/media/homework/${h.id}/files/${f.id}`} className="inline-flex items-center gap-1.5 text-brand hover:underline">
                      <Download className="size-3.5" aria-hidden />
                      {f.fileName}
                      <span className="text-xs text-muted">({fileSize(f.size)})</span>
                    </a>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          {canHandIn && (
            <Card className="p-5">
              <HandInForm studentId={child.studentId} homeworkId={h.id} again={m.state !== "Missing"} />
            </Card>
          )}
        </div>
        {h.acceptsSubmissions && (
          <Card className="space-y-3 p-5">
            <Badge tone={stateTone[m.state]}>{stateLabel[m.state]}</Badge>
            {m.submission ? (
              <>
                <p className="text-sm text-muted">
                  Handed in {formatLagos(m.submission.submittedAt)}
                  {m.submission.isLate && <span className="text-warning"> · late</span>}
                </p>
                {m.submission.text && <p className="rounded-input bg-surface-2 p-3 text-sm whitespace-pre-line">{m.submission.text}</p>}
                {m.submission.files.length > 0 && (
                  <ul className="space-y-1 text-sm">
                    {m.submission.files.map((f) => (
                      <li key={f.id}>
                        <a href={`/api/media/homework/${h.id}/hand-ins/${m.submission!.id}/files/${f.id}`} className="inline-flex items-center gap-1.5 text-brand hover:underline">
                          <Download className="size-3.5" aria-hidden />
                          {f.fileName}
                        </a>
                      </li>
                    ))}
                  </ul>
                )}
                {m.submission.feedback && (
                  <div className="rounded-input border border-border p-3 text-sm">
                    <p className="mb-1 flex items-center gap-1.5 font-medium">
                      <MessageSquare className="size-4 text-brand" aria-hidden />
                      Teacher&apos;s feedback
                    </p>
                    <p className="whitespace-pre-line">{m.submission.feedback}</p>
                  </div>
                )}
              </>
            ) : (
              <p className="text-sm text-muted">{isStudent(user.roles) ? "Not handed in yet." : `${child.name.split(" ")[0]} hasn't handed this in yet. Only the student can hand in, from their own login.`}</p>
            )}
          </Card>
        )}
      </div>
    </>
  );
}
