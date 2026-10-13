import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, ChevronRight } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { statusTone } from "@/lib/validation/students";
import { Avatar, Badge, Card } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { IssueLoginButton } from "@/components/credentials/issue-login";
import { GuardianForm } from "@/components/students/guardian-form";

export const metadata: Metadata = { title: "Guardian" };

export default async function GuardianPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/guardians" className="mb-4 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      All guardians
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        {back}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const result = await load((api) => api.GET("/api/guardians/{id}", { params: { path: { id } } }));
  if (!result.ok && result.problem.status === 404) notFound();
  if (!result.ok) {
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={result.problem} />
        </Card>
      </>
    );
  }
  const g = result.data;
  const childNames = g.children.map((c) => c.fullName.split(" ")[0]).join(", ");

  return (
    <>
      {back}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center">
        <Avatar name={g.fullName} className="size-14 text-lg" />
        <div className="min-w-0 flex-1">
          <h1 className="flex flex-wrap items-center gap-2 font-display text-2xl font-semibold sm:text-3xl">
            {g.fullName}
            {!g.hasLogin && <Badge tone="warning">No login</Badge>}
          </h1>
          <p className="text-sm text-muted">
            Guardian of {g.children.length} {g.children.length === 1 ? "child" : "children"}
          </p>
        </div>
        <IssueLoginButton kind="guardian" id={g.id} fullName={g.fullName} description={childNames ? `Parent of ${childNames}` : "Parent"} hasLogin={g.hasLogin} />
      </div>

      <div className="grid gap-5 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <Card className="p-5">
          <h2 className="mb-1 font-display text-lg font-semibold">Details</h2>
          <p className="mb-4 text-sm text-muted">Shared by all their children. Their username is their email, or their phone if they have no email.</p>
          <GuardianForm guardian={g} />
        </Card>
        <Card className="p-5">
          <h2 className="mb-3 font-display text-lg font-semibold">Children</h2>
          {g.children.length === 0 ? (
            <p className="text-sm text-muted">Not linked to any student.</p>
          ) : (
            <ul className="divide-y divide-border">
              {g.children.map((c) => (
                <li key={c.studentId}>
                  <Link href={`/admin/students/${c.studentId}`} className="flex items-center gap-3 py-3 hover:text-brand">
                    <span className="min-w-0 flex-1">
                      <span className="block font-medium">{c.fullName}</span>
                      <span className="block text-xs text-muted">
                        <span className="font-mono">{c.admissionNumber}</span> · {c.classRoom ?? "No class"} · {c.relationship}
                      </span>
                    </span>
                    <Badge tone={statusTone[c.status]}>{c.status}</Badge>
                    <ChevronRight className="size-4 text-muted" aria-hidden />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </>
  );
}
