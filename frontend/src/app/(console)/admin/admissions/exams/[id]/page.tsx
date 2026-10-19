import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { formatSlotStart, modeLabel } from "@/lib/admissions/exam";
import { applicationStatusLabel, applicationStatusTone } from "@/lib/admissions/rules";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { CandidateScore, PrintButton } from "@/components/admissions/candidate-row-actions";

export const metadata: Metadata = { title: "Exam sitting" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E11.6: one sitting's candidates, as a printable attendance sheet with score entry (admin, teacher). */
export default async function ExamSittingPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const { id } = await params;
  const sp = await searchParams;
  const user = await requireSessionUser();
  const sessionId = one(sp.sessionId);
  const back = (
    <Link href={`/admin/admissions/exams${sessionId ? `?sessionId=${sessionId}` : ""}`} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline print:hidden">
      <ArrowLeft className="size-4" aria-hidden />
      Exam sittings
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        {back}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  if (!GUID.test(id)) notFound();
  const [candidates, slots] = await Promise.all([
    load((api) => api.GET("/api/admissions/exam-slots/{id}/candidates", { params: { path: { id } } })),
    sessionId && GUID.test(sessionId) ? load((api) => api.GET("/api/admissions/exam-slots", { params: { query: { sessionId } } })) : Promise.resolve(null),
  ]);
  if (!candidates.ok && candidates.problem.status === 404) notFound();
  const slot = slots?.ok ? slots.data.find((s) => s.id === id) : undefined;

  return (
    <>
      {back}
      <PageHeader
        title={slot ? formatSlotStart(slot.startsAt) : "Exam sitting"}
        description={slot ? `${slot.durationMinutes} min · ${slot.venue} · ${modeLabel[slot.mode]} · ${slot.booked} of ${slot.capacity} booked` : undefined}
        actions={<PrintButton title={slot ? formatSlotStart(slot.startsAt) : "Exam sitting"} subtitle={slot ? `${slot.venue} · ${modeLabel[slot.mode]}` : undefined} candidates={candidates.ok ? candidates.data : []} />}
      />
      <Card className="overflow-hidden">
        {!candidates.ok ? (
          <ProblemState problem={candidates.problem} />
        ) : candidates.data.length === 0 ? (
          <EmptyState icon={Users} title="Nobody has booked this sitting yet" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[44rem] text-sm">
              <thead className="text-left text-xs uppercase tracking-wider text-muted">
                <tr className="border-b border-border">
                  <th className="px-4 py-2.5 font-medium">Reference</th>
                  <th className="px-3 py-2.5 font-medium">Child</th>
                  <th className="px-3 py-2.5 font-medium">Class</th>
                  <th className="px-3 py-2.5 font-medium print:hidden">Status</th>
                  <th className="px-3 py-2.5 font-medium">Score (%)</th>
                  <th className="hidden px-3 py-2.5 font-medium print:table-cell">Signature</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {candidates.data.map((c) => (
                  <tr key={c.applicationId} className="align-top">
                    <td className="tabular px-4 py-2.5">{c.referenceNumber}</td>
                    <td className="px-3 py-2.5 font-medium">{c.childName}</td>
                    <td className="px-3 py-2.5">{c.classLevel}</td>
                    <td className="px-3 py-2.5 print:hidden">
                      <Badge tone={applicationStatusTone[c.status]}>{applicationStatusLabel[c.status]}</Badge>
                    </td>
                    <td className="px-3 py-2.5">
                      <CandidateScore candidate={c} />
                      <span className="hidden print:inline">{c.score ?? ""}</span>
                    </td>
                    <td className="hidden h-10 w-40 border-b border-dotted border-border px-3 print:table-cell" />
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </>
  );
}
