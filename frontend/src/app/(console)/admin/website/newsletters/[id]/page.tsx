import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { NewsletterEditor } from "@/components/website/newsletter-editor";

export const metadata: Metadata = { title: "Newsletter" };

/** E12.6: write a new newsletter (`/new`) or open one (draft: editable; scheduled or sent: read-only). */
export default async function NewsletterPage({ params }: { params: Promise<{ id: string }> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Newsletter" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const { id } = await params;
  const isNew = id === "new";
  if (!isNew && !GUID.test(id)) notFound();
  const [letter, classes, count] = await Promise.all([
    isNew ? null : load((api) => api.GET("/api/cms/newsletters/{id}", { params: { path: { id } } })),
    load((api) => api.GET("/api/academics/classrooms")),
    isNew ? null : load((api) => api.GET("/api/cms/newsletters/{id}/recipients", { params: { path: { id } } })),
  ]);
  if (letter && !letter.ok && letter.problem.status === 404) notFound();
  const classOptions = classes.ok ? classes.data.map((c) => ({ id: c.id, name: c.name })) : [];

  return (
    <>
      <Link href="/admin/website/newsletters" className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        Newsletters
      </Link>
      <PageHeader title={isNew ? "New newsletter" : letter?.ok ? letter.data.title : "Newsletter"} />
      {letter && !letter.ok ? (
        <Card>
          <ProblemState problem={letter.problem} />
        </Card>
      ) : (
        <NewsletterEditor key={letter?.ok ? letter.data.id : "new"} initial={letter?.ok ? letter.data : null} initialCount={count?.ok ? count.data : null} classes={classOptions} />
      )}
    </>
  );
}
