import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { quotaLabel } from "@/lib/cbt/ai";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { AiReview } from "@/components/cbt/ai-review";

export const metadata: Metadata = { title: "AI drafts" };

/** E16.8: review, edit and save the drafts from one AI request (owner only; others get 404). */
export default async function AiGenerationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/cbt/ai" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Write with AI
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
  const gen = await load((api) => api.GET("/api/cbt/ai/generations/{id}", { params: { path: { id } } }));
  if (!gen.ok) {
    if (gen.problem.status === 404) notFound();
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={gen.problem} />
        </Card>
      </>
    );
  }
  const topics = await load((api) => api.GET("/api/cbt/topics", { params: { query: { subjectId: gen.data.subjectId, classLevelId: gen.data.classLevelId } } }));

  return (
    <>
      {back}
      <PageHeader
        title="Check the drafts"
        description="Edit anything, untick what you don't want, then save. Nothing reaches the bank until you do."
        actions={gen.data.quota.available ? <Badge tone="brand">{quotaLabel(gen.data.quota)}</Badge> : undefined}
      />
      <AiReview initial={gen.data} topics={topics.ok ? topics.data : []} />
    </>
  );
}
