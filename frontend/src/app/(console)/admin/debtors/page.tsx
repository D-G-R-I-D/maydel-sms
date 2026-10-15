import type { Metadata } from "next";
import Link from "next/link";
import { Download, Phone, SearchCheck } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { formatNaira } from "@/lib/finance/rules";
import { Card, PageHeader, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";
import { DebtorFilters } from "@/components/finance/debtor-filters";

export const metadata: Metadata = { title: "Debtors" };

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
const day = (iso: string | null) => (iso ? dateFormat.format(new Date(`${iso}T12:00:00Z`)) : "—");
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E10 part 2a: who still owes this term, most overdue first, with guardian contacts and CSV export. */
export default async function DebtorsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Debtors" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const { terms, selected } = await loadTerms(one(sp.termId));
  if (!selected) {
    return (
      <>
        <PageHeader title="Debtors" />
        <Card>
          <EmptyState title="No terms yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const classRoomId = one(sp.classRoomId);
  const overdueOnly = one(sp.overdueOnly) === "true";
  const [report, classrooms] = await Promise.all([
    load((api) => api.GET("/api/finance/debtors", { params: { query: { termId: selected.id, classRoomId, overdueOnly } } })),
    load((api) => api.GET("/api/academics/classrooms")),
  ]);

  const csv = new URLSearchParams({ termId: selected.id });
  if (classRoomId) csv.set("classRoomId", classRoomId);
  if (overdueOnly) csv.set("overdueOnly", "true");

  return (
    <>
      <PageHeader
        title="Debtors"
        description="Students with a balance this term, most overdue first."
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <TermPicker terms={terms} value={selected.id} />
            <a href={`/api/media/finance/debtors-csv?${csv.toString()}`} download className={buttonClasses("secondary")}>
              <Download className="size-4" aria-hidden />
              Export CSV
            </a>
          </div>
        }
      />
      <div className="mb-4">
        <DebtorFilters classrooms={classrooms.ok ? classrooms.data : []} classRoomId={classRoomId} overdueOnly={overdueOnly} />
      </div>
      {!report.ok ? (
        <Card>
          <ProblemState problem={report.problem} />
        </Card>
      ) : (
        <>
          <div className="mb-4 grid gap-3 sm:grid-cols-3">
            <Card className="p-4">
              <p className="text-sm text-muted">Students owing</p>
              <p className="tabular font-display text-2xl font-semibold">{report.data.count}</p>
            </Card>
            <Card className="p-4">
              <p className="text-sm text-muted">Total outstanding</p>
              <p className="tabular font-display text-2xl font-semibold">{formatNaira(report.data.totalOutstanding)}</p>
            </Card>
            <Card className="p-4">
              <p className="text-sm text-muted">Total overdue</p>
              <p className={cn("tabular font-display text-2xl font-semibold", report.data.totalOverdue > 0 && "text-danger")}>{formatNaira(report.data.totalOverdue)}</p>
            </Card>
          </div>
          <Card className="overflow-hidden">
            {report.data.rows.length === 0 ? (
              <EmptyState icon={SearchCheck} title={overdueOnly ? "Nothing overdue" : "No one owes"}>
                {overdueOnly ? "Every balance is within its due dates." : `No balances for ${selected.label} ${selected.session}.`}
              </EmptyState>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[54rem] text-sm">
                  <thead className="text-left text-xs uppercase tracking-wider text-muted">
                    <tr className="border-b border-border">
                      <th className="px-4 py-2.5 font-medium">Admission no.</th>
                      <th className="px-3 py-2.5 font-medium">Student</th>
                      <th className="px-3 py-2.5 font-medium">Class</th>
                      <th className="px-3 py-2.5 text-right font-medium">Balance</th>
                      <th className="px-3 py-2.5 text-right font-medium">Overdue</th>
                      <th className="px-3 py-2.5 font-medium">Next due</th>
                      <th className="px-4 py-2.5 font-medium">Guardian</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {report.data.rows.map((r) => (
                      <tr key={r.invoiceId} className="hover:bg-surface-2/50">
                        <td className="tabular px-4 py-2.5">{r.admissionNumber}</td>
                        <td className="px-3 py-2.5">
                          <Link href={`/admin/invoices/${r.invoiceId}`} className="font-medium text-brand hover:underline">
                            {r.name}
                          </Link>
                          <span className="tabular block text-xs text-muted">{r.invoiceNumber}</span>
                        </td>
                        <td className="px-3 py-2.5 text-muted">{r.classRoom ?? "—"}</td>
                        <td className="tabular px-3 py-2.5 text-right font-medium">{formatNaira(r.balance)}</td>
                        <td className={cn("tabular px-3 py-2.5 text-right", r.overdue > 0 ? "font-medium text-danger" : "text-muted")}>{formatNaira(r.overdue)}</td>
                        <td className="px-3 py-2.5 whitespace-nowrap">{day(r.nextDueOn)}</td>
                        <td className="px-4 py-2.5">
                          {r.guardian ?? <span className="text-muted">—</span>}
                          {r.guardianPhone && (
                            <a href={`tel:${r.guardianPhone.replace(/[^\d+]/g, "")}`} className="flex items-center gap-1 text-xs text-brand hover:underline">
                              <Phone className="size-3" aria-hidden />
                              {r.guardianPhone}
                            </a>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      )}
    </>
  );
}
