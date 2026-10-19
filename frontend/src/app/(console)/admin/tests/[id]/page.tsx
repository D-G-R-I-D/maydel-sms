import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { loadTestFormData } from "@/lib/cbt/test-data";
import { formatLagos } from "@/lib/time/lagos";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { TestBuilder } from "@/components/cbt/test-builder";
import { TestActions } from "@/components/cbt/test-actions";
import { TestHeader } from "@/components/cbt/test-tabs";

export const metadata: Metadata = { title: "Test" };

/** E16.2: one test. Drafts: details, sections, totals and publish. Published: read-only with Unpublish. */
export default async function TestPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Test" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  if (!GUID.test(id)) notFound();
  const detail = await load((api) => api.GET("/api/cbt/tests/{id}", { params: { path: { id } } }));
  if (!detail.ok) {
    if (detail.problem.status === 404) notFound();
    return (
      <>
        <PageHeader title="Test" />
        <Card>
          <ProblemState problem={detail.problem} />
        </Card>
      </>
    );
  }
  const t = detail.data;
  const [form, topics] = await Promise.all([
    loadTestFormData(t.termId),
    load((api) => api.GET("/api/cbt/topics", { params: { query: { subjectId: t.subjectId, classLevelId: t.classLevelId } } })),
  ]);
  const component = form.components.find((c) => c.id === t.componentId);
  const draft = t.status === "Draft" && t.canEdit;

  return (
    <>
      <TestHeader
        test={t}
        active="build"
        actions={draft && <TestActions test={t} scopes={form.scopes.ok ? form.scopes.data : []} terms={form.terms} components={form.components} arms={form.arms} />}
      />
      <Card className="mb-5 grid gap-4 p-5 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <p className="text-muted">Classes</p>
          <p className="font-medium">{t.classRooms.map((c) => c.name).join(", ") || "None chosen"}</p>
        </div>
        <div>
          <p className="text-muted">Window (Abuja time, WAT)</p>
          <p>
            {formatLagos(t.opensAt)} to {formatLagos(t.closesAt)}
          </p>
        </div>
        <div>
          <p className="text-muted">Sitting</p>
          <p>
            {t.durationMinutes} minutes · {t.maxAttempts} {t.maxAttempts === 1 ? "attempt" : "attempts"}
          </p>
          <p className="text-xs text-muted">
            {t.shuffleQuestions ? "Questions shuffled" : "Questions in order"} · {t.shuffleOptions ? "options shuffled" : "options in order"}
            {t.showScoreOnSubmit ? " · score shown on hand-in" : ""}
          </p>
        </div>
        <div>
          <p className="text-muted">Counts towards</p>
          <p>{t.scaling ? `${t.scaling.component} (out of ${t.scaling.componentMax})` : component ? `${component.name} (out of ${component.maxScore})` : "Nothing (practice test)"}</p>
        </div>
        {t.instructions && (
          <div className="sm:col-span-2 lg:col-span-4">
            <p className="text-muted">Instructions</p>
            <p className="whitespace-pre-wrap">{t.instructions}</p>
          </div>
        )}
      </Card>
      {/* Remount when the details change (header edit, publish), so the builder never shows stale ones. */}
      <TestBuilder key={[t.status, t.title, t.componentId, t.opensAt, t.closesAt, t.classRooms.map((c) => c.id).join()].join("|")} initial={t} components={form.components} topics={topics.ok ? topics.data : []} />
    </>
  );
}
