import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft, ChevronRight, Sparkles } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { assessmentLabel, quotaBlocker, quotaLabel } from "@/lib/cbt/ai";
import { scopeKey } from "@/lib/cbt/tests";
import { formatLagos } from "@/lib/time/lagos";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { AiRequestForm } from "@/components/cbt/ai-request-form";

export const metadata: Metadata = { title: "Write questions with AI" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E16.8: ask Maydel AI Assistant for draft questions from your own material, within your monthly allowance. */
export default async function AiQuestionsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/cbt" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Question bank
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        {back}
        <PageHeader title="Write questions with AI" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const subjectId = one(sp.subjectId);
  const classLevelId = one(sp.classLevelId);
  const [quota, scopes, recent] = await Promise.all([
    load((api) => api.GET("/api/cbt/ai/quota")),
    load((api) => api.GET("/api/cbt/scopes")),
    load((api) => api.GET("/api/cbt/ai/generations")),
  ]);
  const blocker = quota.ok ? quotaBlocker(quota.data) : "Couldn't check your allowance.";

  return (
    <>
      {back}
      <PageHeader
        title="Write questions with AI"
        description="Maydel AI Assistant drafts questions with answer keys from your notes or scheme of work. You check and edit every one before it goes into the bank."
        actions={quota.ok && quota.data.available && quota.data.enabled ? <Badge tone={quota.data.remaining > 0 ? "brand" : "warning"}>{quotaLabel(quota.data)}</Badge> : undefined}
      />
      <div className="grid gap-5 lg:grid-cols-[1fr_18rem] lg:items-start">
        {!scopes.ok ? (
          <Card>
            <ProblemState problem={scopes.problem} />
          </Card>
        ) : scopes.data.length === 0 ? (
          <Card>
            <EmptyState icon={Sparkles} title="No question banks for you yet">
              You can write questions for the subjects you teach. Ask an administrator to assign you.
            </EmptyState>
          </Card>
        ) : (
          <AiRequestForm scopes={scopes.data} initialScope={subjectId && classLevelId ? scopeKey(subjectId, classLevelId) : undefined} blocker={blocker} />
        )}
        <aside aria-labelledby="ai-recent">
          <Card className="p-4">
            <h2 id="ai-recent" className="font-display font-semibold">
              Your recent requests
            </h2>
            {!recent.ok || recent.data.length === 0 ? (
              <p className="mt-1 text-sm text-muted">{recent.ok ? "None yet." : "Couldn't load them."}</p>
            ) : (
              <ul className="mt-2 divide-y divide-border">
                {recent.data.map((g) => (
                  <li key={g.id}>
                    <Link href={`/admin/cbt/ai/${g.id}`} className="group flex items-center gap-2 py-2 text-sm">
                      <span className="min-w-0 flex-1">
                        <span className="block truncate font-medium group-hover:text-brand">
                          {g.subject} · {g.classLevel}
                        </span>
                        <span className="block text-xs text-muted">
                          {assessmentLabel[g.assessmentType]} · {g.count} questions · {g.versions} {g.versions === 1 ? "version" : "versions"}
                        </span>
                        <span className="block text-xs text-muted">{formatLagos(g.createdAt)}</span>
                      </span>
                      <ChevronRight className="size-4 shrink-0 text-muted" aria-hidden />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </aside>
      </div>
    </>
  );
}
