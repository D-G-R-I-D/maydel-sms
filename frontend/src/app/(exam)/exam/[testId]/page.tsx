import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { GUID } from "@/lib/media/binary-proxy";
import { Card } from "@/components/ui/primitives";
import { ProblemState } from "@/components/ui/states";
import { ExamRoom } from "@/components/cbt/exam-room";

export const metadata: Metadata = { title: "Test" };

/** E16.3: instructions, then the paper. Starting (or resuming) happens in the browser, so a refresh resumes. */
export default async function ExamPage({ params }: { params: Promise<{ testId: string }> }) {
  const { testId } = await params;
  if (!GUID.test(testId)) notFound();
  const tests = await load((api) => api.GET("/api/student/tests"));
  if (!tests.ok) {
    return (
      <main className="mx-auto max-w-2xl px-4 py-10">
        <Link href="/portal/tests" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
          <ArrowLeft className="size-4" aria-hidden />
          Tests
        </Link>
        <Card>
          <ProblemState problem={tests.problem} />
        </Card>
      </main>
    );
  }
  const test = tests.data.find((t) => t.id === testId);
  if (!test) notFound();
  return <ExamRoom test={test} />;
}
