import type { Metadata } from "next";
import Link from "next/link";
import { Inbox } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import type { ApplicationStatus } from "@/lib/api/types";
import { loadAdmissionSessions } from "@/lib/admissions/sessions";
import { applicationStatusLabel, applicationStatusTone } from "@/lib/admissions/rules";
import { formatNaira } from "@/lib/finance/rules";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { SessionPicker } from "@/components/admissions/session-picker";
import { AdmissionsTabs } from "@/components/admissions/admissions-tabs";
import { ApplicationFilters } from "@/components/admissions/application-filters";

export const metadata: Metadata = { title: "Applications" };

const PAGE_SIZE = 25;
const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E11.4: applications for a session, with search, status and class filters (Admin, Bursar). */
export default async function ApplicationsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Admissions" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const { sessions, selected } = await loadAdmissionSessions(one(sp.sessionId));
  if (!selected) {
    return (
      <>
        <PageHeader title="Admissions" />
        <Card>
          <EmptyState title="No sessions yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const statusParam = one(sp.status);
  const status = statusParam && statusParam in applicationStatusLabel ? (statusParam as ApplicationStatus) : undefined;
  const classLevelId = one(sp.classLevelId);
  const search = one(sp.search)?.slice(0, 100);
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const [apps, levels] = await Promise.all([
    load((api) => api.GET("/api/admissions/applications", { params: { query: { sessionId: selected.id, status, classLevelId, search, page, pageSize: PAGE_SIZE } } })),
    load((api) => api.GET("/api/academics/class-levels")),
  ]);
  const href = (p: number) => {
    const q = new URLSearchParams({ sessionId: selected.id });
    if (status) q.set("status", status);
    if (classLevelId) q.set("classLevelId", classLevelId);
    if (search) q.set("search", search);
    if (p > 1) q.set("page", String(p));
    return `/admin/admissions?${q.toString()}`;
  };

  return (
    <>
      <PageHeader title="Admissions" description="Families who bought the admission form, and where each application is." actions={<SessionPicker sessions={sessions} value={selected.id} />} />
      <AdmissionsTabs active="applications" sessionId={selected.id} isAdmin={hasAnyRole(user.roles, ["Admin"])} isBursar={hasAnyRole(user.roles, ["Bursar"])} />
      <ApplicationFilters levels={levels.ok ? levels.data.map((l) => ({ id: l.id, name: l.name })) : []} />
      <Card className="overflow-hidden">
        {!apps.ok ? (
          <ProblemState problem={apps.problem} />
        ) : apps.data.items.length === 0 ? (
          <EmptyState icon={Inbox} title={status || classLevelId || search ? "No applications match" : "No applications yet"}>
            {status || classLevelId || search ? "Try another search or filter." : "Applications appear here when families buy the form."}
          </EmptyState>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[56rem] text-sm">
                <thead className="text-left text-xs uppercase tracking-wider text-muted">
                  <tr className="border-b border-border">
                    <th className="px-4 py-2.5 font-medium">Reference</th>
                    <th className="px-3 py-2.5 font-medium">Child</th>
                    <th className="px-3 py-2.5 font-medium">Class</th>
                    <th className="px-3 py-2.5 font-medium">Parent</th>
                    <th className="px-3 py-2.5 font-medium">Status</th>
                    <th className="px-3 py-2.5 font-medium">Form paid</th>
                    <th className="px-4 py-2.5 font-medium">Exam by</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {apps.data.items.map((a) => (
                    <tr key={a.id} className="align-top">
                      <td className="tabular px-4 py-2.5 font-medium">
                        <Link href={`/admin/admissions/${a.id}`} className="text-brand hover:underline">
                          {a.referenceNumber}
                        </Link>
                      </td>
                      <td className="px-3 py-2.5">
                        {a.childFirstName} {a.childLastName}
                        {a.childDateOfBirth && <span className="block text-xs text-muted">Born {day(a.childDateOfBirth)}</span>}
                      </td>
                      <td className="px-3 py-2.5">{a.classLevel}</td>
                      <td className="px-3 py-2.5">
                        {a.parentName}
                        <a href={`mailto:${a.email}`} className="block text-xs text-brand break-all hover:underline">
                          {a.email}
                        </a>
                        <a href={`tel:${a.phone.replace(/[^0-9+]/g, "")}`} className="block text-xs text-brand hover:underline">
                          {a.phone}
                        </a>
                      </td>
                      <td className="px-3 py-2.5">
                        <Badge tone={applicationStatusTone[a.status]}>{applicationStatusLabel[a.status]}</Badge>
                      </td>
                      <td className="px-3 py-2.5 whitespace-nowrap">
                        {a.formPaidAt ? (
                          <>
                            {day(a.formPaidAt)}
                            <span className="block text-xs text-muted">
                              {a.formPaymentMethod === "SchoolCode" ? "Form code" : "Online"} · {formatNaira(a.formFee)}
                            </span>
                          </>
                        ) : (
                          <span className="text-muted">Not yet</span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 whitespace-nowrap">{a.examWindowEndsOn ? day(a.examWindowEndsOn) : <span className="text-muted">—</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-border px-4 py-3">
              <Pagination page={apps.data.page} pageSize={apps.data.pageSize} totalCount={apps.data.totalCount} hrefFor={href} noun="applications" />
            </div>
          </>
        )}
      </Card>
    </>
  );
}
