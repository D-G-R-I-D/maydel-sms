import type { Metadata } from "next";
import Link from "next/link";
import { EyeOff, MonitorSmartphone, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { loadTestForPage } from "@/lib/cbt/test-page";
import { TEACHER_MARKED, releaseBlocker, rowStateLabel, rowStateTone } from "@/lib/cbt/marking";
import { firstLine, kindLabel } from "@/lib/cbt/questions";
import { formatMarks } from "@/lib/cbt/tests";
import { formatLagos } from "@/lib/time/lagos";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TestHeader } from "@/components/cbt/test-tabs";
import { ReleaseButton } from "@/components/cbt/release-button";

export const metadata: Metadata = { title: "Results and marking" };

/** E16.4: who sat it, who needs marking, marking by student or by question, and releasing results. */
export default async function TestResultsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const test = await loadTestForPage(id);
  if (!test || !test.ok) {
    return (
      <>
        <PageHeader title="Results and marking" />
        <Card>{test ? <ProblemState problem={test.problem} /> : <AccessDenied />}</Card>
      </>
    );
  }
  const t = test.data;
  const [rows, analytics] = await Promise.all([
    load((api) => api.GET("/api/cbt/tests/{id}/attempts", { params: { path: { id } } })),
    load((api) => api.GET("/api/cbt/tests/{id}/analytics", { params: { path: { id } } })),
  ]);
  // Rendered per request: whether the window has closed is as of now.
  const now = new Date().getTime();
  const students = rows.ok ? rows.data.students : [];
  const submitted = students.filter((s) => s.state === "Submitted").length;
  const blocker = rows.ok
    ? releaseBlocker({ released: Boolean(t.resultsReleasedAt), closesAt: t.closesAt, now, sitting: rows.data.sitting, needsMarking: rows.data.needsMarking, submitted })
    : "Couldn't load the attempts.";
  const toMark = analytics.ok ? analytics.data.items.filter((i) => TEACHER_MARKED.has(i.kind) && i.answered > 0) : [];

  return (
    <>
      <TestHeader test={t} active="results" />
      {!rows.ok ? (
        <Card>
          <ProblemState problem={rows.problem} />
        </Card>
      ) : (
        <div className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {(
              [
                ["Students", students.length],
                ["Sitting now", rows.data.sitting],
                ["Handed in", submitted],
                ["Answers to mark", rows.data.needsMarking],
              ] as const
            ).map(([label, n]) => (
              <Card key={label} className="p-4">
                <p className="text-sm text-muted">{label}</p>
                <p className="tabular font-display text-2xl font-semibold">{n}</p>
              </Card>
            ))}
          </div>

          <Card className="flex flex-wrap items-center gap-4 p-5">
            <div className="min-w-0 flex-1">
              <h2 className="font-display text-lg font-semibold">{t.resultsReleasedAt ? "Results released" : "Release results"}</h2>
              <p className="text-sm text-muted">
                {t.resultsReleasedAt
                  ? `Released ${formatLagos(t.resultsReleasedAt)}. Students and parents can see scores and comments; marks can't change now.`
                  : (blocker ?? "Everything is marked. Check the marks, then release them to students and parents.")}
              </p>
            </div>
            {!t.resultsReleasedAt && <ReleaseButton testId={t.id} disabledReason={blocker} students={submitted} />}
          </Card>

          {toMark.length > 0 && (
            <Card className="p-5">
              <h2 className="font-display text-lg font-semibold">Mark by question</h2>
              <p className="text-sm text-muted">Mark one question for everyone at a time, so marking stays consistent. Unmarked answers come first.</p>
              <ul className="mt-3 divide-y divide-border">
                {toMark.map((q) => (
                  <li key={q.questionId}>
                    <Link href={`/admin/tests/${t.id}/questions/${q.questionId}`} className="group flex flex-wrap items-center gap-2 py-2.5">
                      <span className="min-w-0 flex-1 text-sm break-words group-hover:text-brand">{firstLine(q.text, 120)}</span>
                      <Badge>{kindLabel[q.kind]}</Badge>
                      <span className="tabular text-xs text-muted">
                        {q.section} · {q.answered} {q.answered === 1 ? "answer" : "answers"}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            </Card>
          )}

          <Card className="overflow-hidden">
            {students.length === 0 ? (
              <EmptyState icon={Users} title="No students in these classes">
                Students in the test&apos;s classes appear here.
              </EmptyState>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[48rem] text-sm">
                  <thead className="text-left text-xs uppercase tracking-wider text-muted">
                    <tr className="border-b border-border">
                      <th className="px-4 py-2.5 font-medium">Student</th>
                      <th className="px-3 py-2.5 font-medium">State</th>
                      <th className="px-3 py-2.5 font-medium">Handed in</th>
                      <th className="px-3 py-2.5 font-medium">Focus</th>
                      <th className="px-3 py-2.5 text-right font-medium">Computer</th>
                      <th className="px-3 py-2.5 text-right font-medium">Score /{formatMarks(rows.data.totalMarks)}</th>
                      <th className="px-4 py-2.5 font-medium">
                        <span className="sr-only">Marking</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {students.map((s) => (
                      <tr key={s.studentId} className="align-top">
                        <td className="px-4 py-2.5">
                          <span className="font-medium">{s.name}</span>
                          <span className="block text-xs text-muted">
                            <span className="font-mono">{s.admissionNumber}</span>
                            {s.classRoom ? ` · ${s.classRoom}` : ""}
                          </span>
                        </td>
                        <td className="px-3 py-2.5">
                          <Badge tone={rowStateTone[s.state]}>{rowStateLabel[s.state]}</Badge>
                          {s.attempts > 1 && <span className="block text-xs text-muted">{s.attempts} attempts</span>}
                        </td>
                        <td className="px-3 py-2.5 whitespace-nowrap">
                          {s.submittedAt ? formatLagos(s.submittedAt) : s.startedAt ? `Started ${formatLagos(s.startedAt)}` : "—"}
                          {s.autoSubmitted && <span className="block text-xs text-muted">When time ran out</span>}
                        </td>
                        <td className="px-3 py-2.5">
                          <span className="flex gap-3 text-muted">
                            {s.blurs > 0 && (
                              <span className="inline-flex items-center gap-1" title={`Left the test ${s.blurs} ${s.blurs === 1 ? "time" : "times"}`}>
                                <EyeOff className="size-4" aria-hidden />
                                <span className="tabular">{s.blurs}</span>
                                <span className="sr-only">{s.blurs === 1 ? "time" : "times"} away from the test</span>
                              </span>
                            )}
                            {s.takeOvers > 0 && (
                              <span className="inline-flex items-center gap-1" title={`Opened on another tab or device ${s.takeOvers} ${s.takeOvers === 1 ? "time" : "times"}`}>
                                <MonitorSmartphone className="size-4" aria-hidden />
                                <span className="tabular">{s.takeOvers}</span>
                                <span className="sr-only">{s.takeOvers === 1 ? "takeover" : "takeovers"} from another tab or device</span>
                              </span>
                            )}
                            {s.blurs === 0 && s.takeOvers === 0 && "—"}
                          </span>
                        </td>
                        <td className="tabular px-3 py-2.5 text-right">{s.autoScore === null ? "—" : formatMarks(s.autoScore)}</td>
                        <td className="tabular px-3 py-2.5 text-right font-medium">{s.score === null ? "—" : formatMarks(s.score)}</td>
                        <td className="px-4 py-2.5 text-right whitespace-nowrap">
                          {s.attemptId && s.state === "Submitted" && (
                            <Link href={`/admin/tests/${t.id}/attempts/${s.attemptId}`} className="inline-flex items-center gap-2 text-brand hover:underline">
                              {s.needsMarking > 0 ? (
                                <>
                                  Mark <Badge tone="warning">{s.needsMarking} to mark</Badge>
                                </>
                              ) : (
                                "Review"
                              )}
                            </Link>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>
      )}
    </>
  );
}
