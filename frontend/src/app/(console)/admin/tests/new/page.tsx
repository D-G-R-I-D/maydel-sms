import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTestFormData } from "@/lib/cbt/test-data";
import { testsHref } from "@/lib/cbt/tests";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TestHeaderForm } from "@/components/cbt/test-header-form";

export const metadata: Metadata = { title: "New test" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E16.2 header step for a new test; sections come next, on the test's page. */
export default async function NewTestPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const sp = await searchParams;
  const back = (termId?: string) => (
    <Link href={testsHref(termId)} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Tests &amp; exams
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        {back()}
        <PageHeader title="New test" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const { scopes, terms, selected, components, arms } = await loadTestFormData(one(sp.termId));

  return (
    <>
      {back(selected?.id)}
      <PageHeader title="New test" description="First the details: who sits it, when, and what it counts towards. Then you add sections of questions." />
      <Card className="p-5">
        {!scopes.ok ? (
          <ProblemState problem={scopes.problem} />
        ) : !selected ? (
          <EmptyState title="No terms yet">Set up the school calendar under Academics first.</EmptyState>
        ) : scopes.data.length === 0 ? (
          <EmptyState title="No subjects to set tests for">You can set tests for the subjects you teach. Ask an administrator to assign you.</EmptyState>
        ) : (
          <TestHeaderForm test={null} termId={selected.id} scopes={scopes.data} terms={terms} components={components} arms={arms} />
        )}
      </Card>
    </>
  );
}
