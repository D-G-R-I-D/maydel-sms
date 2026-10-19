import type { Metadata } from "next";
import { BarChart3, Check } from "lucide-react";
import { load } from "@/lib/api/server";
import { loadTestForPage } from "@/lib/cbt/test-page";
import { facilityTone, percentOf } from "@/lib/cbt/marking";
import { firstLine, kindLabel } from "@/lib/cbt/questions";
import { formatMarks } from "@/lib/cbt/tests";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TestHeader } from "@/components/cbt/test-tabs";
import { Columns, HBar } from "@/components/cbt/bars";

export const metadata: Metadata = { title: "Test analytics" };

/** E16.7: how the class did (tiles, distribution, by arm) and which questions were hardest. */
export default async function TestAnalyticsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const test = await loadTestForPage(id);
  if (!test || !test.ok) {
    return (
      <>
        <PageHeader title="Test analytics" />
        <Card>{test ? <ProblemState problem={test.problem} /> : <AccessDenied />}</Card>
      </>
    );
  }
  const a = await load((api) => api.GET("/api/cbt/tests/{id}/analytics", { params: { path: { id } } }));
  const sections = a.ok ? [...new Set(a.data.items.map((i) => i.section))] : [];

  return (
    <>
      <TestHeader test={test.data} active="analytics" />
      {!a.ok ? (
        <Card>
          <ProblemState problem={a.problem} />
        </Card>
      ) : a.data.students === 0 ? (
        <Card>
          <EmptyState icon={BarChart3} title="No marked attempts yet">
            Analytics appear once students have handed in and every answer is marked.
          </EmptyState>
        </Card>
      ) : (
        <div className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            {(
              [
                ["Students", a.data.students, null],
                ["Mean", a.data.mean, a.data.mean],
                ["Median", a.data.median, a.data.median],
                ["Highest", a.data.highest, a.data.highest],
                ["Lowest", a.data.lowest, a.data.lowest],
              ] as const
            ).map(([label, v, score]) => (
              <Card key={label} className="p-4">
                <p className="text-sm text-muted">{label}</p>
                <p className="tabular font-display text-2xl font-semibold">{v === null ? "—" : formatMarks(v)}</p>
                {score !== null && <p className="tabular text-xs text-muted">of {formatMarks(a.data.totalMarks)} · {percentOf(score, a.data.totalMarks)}%</p>}
              </Card>
            ))}
          </div>

          <div className="grid gap-5 lg:grid-cols-[2fr_1fr]">
            <Card className="p-5">
              <h2 className="mb-3 font-display text-lg font-semibold">Score distribution</h2>
              <Columns caption="Students per score band" data={a.data.distribution.map((b) => ({ label: `${b.fromPercent}–${b.toPercent}%`, value: b.students }))} />
            </Card>
            <Card className="p-5">
              <h2 className="mb-3 font-display text-lg font-semibold">By class</h2>
              <table className="w-full text-sm">
                <thead className="text-left text-xs uppercase tracking-wider text-muted">
                  <tr>
                    <th className="py-1.5 font-medium">Class</th>
                    <th className="py-1.5 text-right font-medium">Students</th>
                    <th className="py-1.5 text-right font-medium">Mean</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {a.data.byClass.map((c) => (
                    <tr key={c.classRoom}>
                      <td className="py-2">{c.classRoom}</td>
                      <td className="tabular py-2 text-right">{c.students}</td>
                      <td className="tabular py-2 text-right font-medium">{formatMarks(c.mean)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          </div>

          <Card className="p-5">
            <h2 className="font-display text-lg font-semibold">Questions</h2>
            <p className="text-sm text-muted">Facility is the share of the marks students earned: low means hard. Hardest first in each section. Open a question to see which options were picked.</p>
            {sections.map((s) => (
              <section key={s} className="mt-4" aria-label={s}>
                <h3 className="mb-2 text-sm font-semibold">{s}</h3>
                <ul className="divide-y divide-border rounded-input border border-border">
                  {a.data.items
                    .filter((i) => i.section === s)
                    .map((q) => {
                      const tone = facilityTone(q.facility);
                      const pct = q.facility === null ? null : Math.round(q.facility * 100);
                      return (
                        <li key={q.questionId}>
                          <details className="group">
                            <summary className="grid cursor-pointer list-none gap-2 px-3 py-2.5 sm:grid-cols-[1fr_12rem] sm:items-center">
                              <span className="min-w-0">
                                <span className="block text-sm break-words">{firstLine(q.text, 140)}</span>
                                <span className="mt-0.5 flex flex-wrap gap-1.5 text-xs text-muted">
                                  <Badge>{kindLabel[q.kind]}</Badge>
                                  {q.answered} of {q.responses} answered
                                </span>
                              </span>
                              <span className="flex items-center gap-2">
                                <HBar ratio={q.facility ?? 0} tone={tone} className="flex-1" />
                                <span className="tabular w-16 text-right text-sm font-medium">{pct === null ? "—" : `${pct}%`}</span>
                              </span>
                            </summary>
                            {q.options.length > 0 && (
                              <ul className="space-y-1.5 border-t border-border px-3 py-3" aria-label="Option picks">
                                {q.options.map((o) => (
                                  <li key={o.optionId} className="grid grid-cols-[1fr_8rem_3rem] items-center gap-2 text-sm">
                                    <span className="flex min-w-0 items-center gap-1.5">
                                      {o.isCorrect && <Check className="size-4 shrink-0 text-success" aria-label="Correct option" />}
                                      <span className="truncate">{o.text}</span>
                                    </span>
                                    <HBar ratio={q.responses > 0 ? o.picks / q.responses : 0} tone={o.isCorrect ? "success" : "neutral"} />
                                    <span className="tabular text-right text-muted">{o.picks}</span>
                                  </li>
                                ))}
                              </ul>
                            )}
                          </details>
                        </li>
                      );
                    })}
                </ul>
              </section>
            ))}
          </Card>
        </div>
      )}
    </>
  );
}
