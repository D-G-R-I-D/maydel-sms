import type { Metadata } from "next";
import { load } from "@/lib/api/server";
import { loadTestForPage } from "@/lib/cbt/test-page";
import { pushStateLabel, pushStateTone } from "@/lib/cbt/marking";
import { formatMarks } from "@/lib/cbt/tests";
import { formatLagos } from "@/lib/time/lagos";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, Alert, EmptyState, ProblemState } from "@/components/ui/states";
import { TestHeader } from "@/components/cbt/test-tabs";
import { PushButton } from "@/components/cbt/push-button";

export const metadata: Metadata = { title: "Grade sheet" };

/** E16.5: preview raw → scaled scores against the grade sheet, then write them to the component. */
export default async function TestGradebookPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const test = await loadTestForPage(id);
  if (!test || !test.ok) {
    return (
      <>
        <PageHeader title="Grade sheet" />
        <Card>{test ? <ProblemState problem={test.problem} /> : <AccessDenied />}</Card>
      </>
    );
  }
  const t = test.data;
  const preview = t.resultsReleasedAt && t.componentId ? await load((api) => api.GET("/api/cbt/tests/{id}/gradebook", { params: { path: { id } } })) : null;

  return (
    <>
      <TestHeader test={t} active="gradebook" />
      {!t.componentId ? (
        <Card>
          <EmptyState title="A practice test">Practice tests don&apos;t go to the grade sheet.</EmptyState>
        </Card>
      ) : !t.resultsReleasedAt || !preview ? (
        <Card>
          <EmptyState title="Release the results first">Scores go to the grade sheet after you release them to students.</EmptyState>
        </Card>
      ) : !preview.ok ? (
        <Card>
          <ProblemState problem={preview.problem} />
        </Card>
      ) : (
        <div className="space-y-4">
          <Card className="flex flex-wrap items-center gap-4 p-5">
            <div className="min-w-0 flex-1">
              <h2 className="font-display text-lg font-semibold">
                {preview.data.component} (out of {formatMarks(preview.data.componentMax)})
              </h2>
              <p className="text-sm text-muted">
                Each score is scaled: raw ÷ {formatMarks(preview.data.totalMarks)} × {formatMarks(preview.data.componentMax)}, to 2 decimals. Only this component changes; reports already published are never
                changed.
              </p>
              {t.gradebookPushedAt && <p className="mt-1 text-sm">Sent {formatLagos(t.gradebookPushedAt)}. You can send again after changes.</p>}
            </div>
            <PushButton testId={t.id} ready={preview.data.rows.filter((r) => r.state === "Ready").length} component={preview.data.component} />
          </Card>
          {preview.data.rows.some((r) => r.state === "ReportPublished") && (
            <Alert tone="warning" title="Some reports are already published">
              Those students keep the score on their published report.
            </Alert>
          )}
          <Card className="overflow-hidden">
            {preview.data.rows.length === 0 ? (
              <EmptyState title="No scores yet">Students who handed in appear here.</EmptyState>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[40rem] text-sm">
                  <thead className="text-left text-xs uppercase tracking-wider text-muted">
                    <tr className="border-b border-border">
                      <th className="px-4 py-2.5 font-medium">Student</th>
                      <th className="px-3 py-2.5 text-right font-medium">Raw /{formatMarks(preview.data.totalMarks)}</th>
                      <th className="px-3 py-2.5 text-right font-medium">Scaled /{formatMarks(preview.data.componentMax)}</th>
                      <th className="px-3 py-2.5 text-right font-medium">In the grade sheet</th>
                      <th className="px-4 py-2.5 font-medium">State</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {preview.data.rows.map((r) => (
                      <tr key={r.studentId}>
                        <td className="px-4 py-2.5">
                          {r.name}
                          {r.classRoom && <span className="block text-xs text-muted">{r.classRoom}</span>}
                        </td>
                        <td className="tabular px-3 py-2.5 text-right">{formatMarks(r.raw)}</td>
                        <td className="tabular px-3 py-2.5 text-right font-medium">{formatMarks(r.scaled)}</td>
                        <td className="tabular px-3 py-2.5 text-right">{r.current === null ? "—" : formatMarks(r.current)}</td>
                        <td className="px-4 py-2.5">
                          <Badge tone={pushStateTone[r.state]}>{pushStateLabel[r.state]}</Badge>
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
