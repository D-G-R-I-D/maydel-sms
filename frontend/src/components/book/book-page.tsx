import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { GUID } from "@/lib/media/binary-proxy";
import { Card, PageHeader } from "@/components/ui/primitives";
import { ProblemState } from "@/components/ui/states";
import { BookThread } from "./book-thread";

/** E17.2: one student's book, for the school side (console) or the family side (portal). The API decides access. */
export async function BookPageView({ studentId, viewer, back }: { studentId: string; viewer: "School" | "Family"; back: { href: string; label: string } }) {
  if (!GUID.test(studentId)) notFound();
  const page = await load((api) => api.GET("/api/books/{studentId}", { params: { path: { studentId } } }));
  if (!page.ok && page.problem.status === 404) notFound();
  return (
    <>
      <Link href={back.href} className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        {back.label}
      </Link>
      <PageHeader title={page.ok ? page.data.student : "Communication book"} description={page.ok && page.data.classRoom ? `${page.data.classRoom} · between the class teacher and the family` : undefined} />
      {page.ok ? (
        <BookThread key={page.data.studentId} page={page.data} viewer={viewer} />
      ) : (
        <Card>
          <ProblemState problem={page.problem} />
        </Card>
      )}
    </>
  );
}
