import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, CalendarClock, FileText } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { applicationStatusLabel, applicationStatusTone } from "@/lib/admissions/rules";
import { firstName } from "@/lib/flash";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { ApplyAnotherChild, PayFormButton, RedeemCodeForm } from "@/components/admissions/application-actions";

export const metadata: Metadata = { title: "Your applications" };

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));

/** E11.4: a family's applications: pay the form (online or with a code), then the next steps. */
export default async function AdmissionsHomePage() {
  const user = await requireSessionUser();
  const [apps, info] = await Promise.all([load((api) => api.GET("/api/admissions/my-applications")), load((api) => api.GET("/api/public/admissions"))]);
  const open = info.ok ? info.data.filter((s) => s.isOpen) : [];

  return (
    <>
      <PageHeader
        title={`Welcome, ${firstName(user.fullName) || user.fullName}`}
        description="Your children's applications to Maydel Spring Schools."
        actions={open.length > 0 ? <ApplyAnotherChild sessions={open} /> : undefined}
      />
      {!apps.ok ? (
        <Card>
          <ProblemState problem={apps.problem} />
        </Card>
      ) : apps.data.length === 0 ? (
        <Card>
          <EmptyState icon={FileText} title="No applications yet">
            {open.length > 0 ? "Use “Apply for another child” to start one." : "Admissions are closed right now. Please check back later."}
          </EmptyState>
        </Card>
      ) : (
        <ul className="space-y-4">
          {apps.data.map((a) => (
            <li key={a.id}>
              <Card className="p-5">
                <div className="flex flex-wrap items-start gap-3">
                  <div className="min-w-0 flex-1">
                    <h2 className="font-display text-lg font-semibold">
                      {a.childFirstName} {a.childLastName}
                    </h2>
                    <p className="text-sm text-muted">
                      {a.classLevel} · {a.session} · <span className="tabular">{a.referenceNumber}</span>
                    </p>
                  </div>
                  <Badge tone={applicationStatusTone[a.status]}>{applicationStatusLabel[a.status]}</Badge>
                </div>

                {a.status === "AwaitingPayment" && (
                  <div className="mt-4 grid gap-5 border-t border-border pt-4 sm:grid-cols-2">
                    <div>
                      <p className="mb-2 text-sm text-muted">Pay the form fee to unlock the application form.</p>
                      <PayFormButton application={a} />
                    </div>
                    <RedeemCodeForm applicationId={a.id} />
                  </div>
                )}

                {a.examWindowEndsOn && a.status === "FormPaid" && (
                  <p className="mt-4 flex items-center gap-2 rounded-input bg-brand/5 p-3 text-sm">
                    <CalendarClock className="size-4 shrink-0 text-brand" aria-hidden />
                    Form paid. Fill in the form and book the entrance exam by <span className="font-medium">{day(a.examWindowEndsOn)}</span>.
                  </p>
                )}

                {a.status !== "AwaitingPayment" && (
                  <div className="mt-4 flex justify-end">
                    <Link href={`/admissions/${a.id}`} className={buttonClasses(a.status === "FormPaid" ? "primary" : "secondary", "sm")}>
                      {a.status === "FormPaid" ? "Fill in the application form" : "View application"}
                      <ArrowRight className="size-4" aria-hidden />
                    </Link>
                  </div>
                )}
              </Card>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
