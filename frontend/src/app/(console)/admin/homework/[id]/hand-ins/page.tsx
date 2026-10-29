import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Download, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { formatLagos } from "@/lib/time/lagos";
import { fileSize, stateLabel, stateTone } from "@/lib/homework/homework";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { ReviewHandIn } from "@/components/homework/review-hand-in";

export const metadata: Metadata = { title: "Hand-ins" };

/** E17.1 staff: every student in the class with their hand-in state; open one to read it and review. */
export default async function HandInsPage({ params }: { params: Promise<{ id: string }> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Hand-ins" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const { id } = await params;
  if (!GUID.test(id)) notFound();
  const [hw, roster] = await Promise.all([load((api) => api.GET("/api/homework/{id}", { params: { path: { id } } })), load((api) => api.GET("/api/homework/{id}/hand-ins", { params: { path: { id } } }))]);
  if (!hw.ok && hw.problem.status === 404) notFound();
  const counts = roster.ok ? { in: roster.data.filter((r) => r.state !== "Missing").length, total: roster.data.length } : null;

  return (
    <>
      <Link href={`/admin/homework/${id}`} className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        {hw.ok ? hw.data.title : "Homework"}
      </Link>
      <PageHeader title="Hand-ins" description={hw.ok ? `${hw.data.classRoom} · ${hw.data.subject ?? "General"}${counts ? ` · ${counts.in} of ${counts.total} handed in` : ""}` : undefined} />
      {!roster.ok ? (
        <Card>
          <ProblemState problem={roster.problem} />
        </Card>
      ) : roster.data.length === 0 ? (
        <Card>
          <EmptyState icon={Users} title="No students in this class" />
        </Card>
      ) : (
        <Card className="divide-y divide-border p-0">
          {roster.data.map((r) => (
            <details key={r.studentId} className="group">
              <summary className="flex cursor-pointer list-none items-center gap-3 px-5 py-3 hover:bg-surface-2">
                <span className="min-w-0 flex-1">
                  <span className="font-medium">{r.name}</span>
                  <span className="ml-2 font-mono text-xs text-muted">{r.admissionNumber}</span>
                </span>
                {r.submission?.isLate && <Badge tone="warning">Late</Badge>}
                <Badge tone={stateTone[r.state]}>{stateLabel[r.state]}</Badge>
              </summary>
              {r.submission ? (
                <div className="space-y-3 bg-surface-2/40 px-5 pt-1 pb-4 text-sm">
                  <p className="text-xs text-muted">Handed in {formatLagos(r.submission.submittedAt)}</p>
                  {r.submission.text && <p className="whitespace-pre-line rounded-input bg-surface p-3">{r.submission.text}</p>}
                  {r.submission.files.length > 0 && (
                    <ul className="space-y-1">
                      {r.submission.files.map((f) => (
                        <li key={f.id}>
                          <a href={`/api/media/homework/${id}/hand-ins/${r.submission!.id}/files/${f.id}`} className="inline-flex items-center gap-1.5 text-brand hover:underline">
                            <Download className="size-3.5" aria-hidden />
                            {f.fileName}
                            <span className="text-xs text-muted">({fileSize(f.size)})</span>
                          </a>
                        </li>
                      ))}
                    </ul>
                  )}
                  <ReviewHandIn homeworkId={id} submission={r.submission} studentName={r.name} />
                </div>
              ) : (
                <p className="px-5 pb-4 text-sm text-muted">Nothing handed in yet.</p>
              )}
            </details>
          ))}
        </Card>
      )}
    </>
  );
}
