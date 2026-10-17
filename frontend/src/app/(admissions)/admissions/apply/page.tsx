import type { Metadata } from "next";
import Link from "next/link";
import { Download, DoorClosed, FileText } from "lucide-react";
import { load } from "@/lib/api/server";
import { getSessionUser } from "@/lib/auth/session";
import { formatNaira } from "@/lib/finance/rules";
import { Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { ApplyForm } from "@/components/admissions/apply-form";

export const metadata: Metadata = { title: "Buy an admission form" };

/** E11.4: public "Buy admission form" (linked from the school website). */
export default async function ApplyPage() {
  const [user, info] = await Promise.all([getSessionUser(), load((api) => api.GET("/api/public/admissions"))]);
  const header = <PageHeader title="Apply to Maydel Spring Schools" description="Buy the admission form online. You'll create a family account to fill in the form, book the entrance exam and see the outcome." />;

  if (user) {
    return (
      <>
        {header}
        <Card className="p-6">
          <p>You&apos;re signed in as {user.fullName}.</p>
          <Link href="/admissions" className={`${buttonClasses("primary")} mt-4`}>
            Go to your applications
          </Link>
        </Card>
      </>
    );
  }
  if (!info.ok) {
    return (
      <>
        {header}
        <Card>
          <ProblemState problem={info.problem} />
        </Card>
      </>
    );
  }
  const open = info.data.filter((s) => s.isOpen);
  if (open.length === 0) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={DoorClosed} title="Admissions are closed right now">
            Please check back later, or contact the school office.
          </EmptyState>
        </Card>
      </>
    );
  }
  const first = open[0];

  return (
    <>
      {header}
      <div className="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <Card className="p-5 sm:p-6">
          <ApplyForm sessions={open} />
        </Card>
        <aside className="space-y-4">
          <Card className="p-5">
            <h2 className="flex items-center gap-2 font-display text-lg font-semibold">
              <FileText className="size-5 text-brand" aria-hidden />
              {first.session} admissions
            </h2>
            <dl className="mt-3 space-y-1.5 text-sm">
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Form fee</dt>
                <dd className="tabular font-medium">{first.formFee > 0 ? formatNaira(first.formFee) : "Free"}</dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Book the exam within</dt>
                <dd className="font-medium">{first.examWindowDays} days of paying</dd>
              </div>
            </dl>
            {first.instructions && <p className="mt-3 text-sm whitespace-pre-line">{first.instructions}</p>}
          </Card>
          <Card className="p-5 text-sm">
            <p className="font-medium">Prefer paper?</p>
            <p className="mt-1 text-muted">Download the blank form to see what&apos;s asked. You&apos;ll still need a form code from the school to apply.</p>
            <a href={`/api/media/admissions/blank-form?sessionId=${first.sessionId}`} download className={`${buttonClasses("secondary", "sm")} mt-3`}>
              <Download className="size-4" aria-hidden />
              Download blank form
            </a>
          </Card>
          <p className="px-1 text-sm text-muted">
            Already applied?{" "}
            <Link href="/login?next=%2Fadmissions" className="font-medium text-brand hover:underline">
              Sign in
            </Link>
          </p>
        </aside>
      </div>
    </>
  );
}
