import type { Metadata } from "next";
import Link from "next/link";
import { Landmark } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import type { ProofStatus } from "@/lib/api/types";
import { formatNaira } from "@/lib/finance/rules";
import { proofStatusTone } from "@/lib/finance/proofs";
import { schoolToday } from "@/lib/validation/students";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { ProofReviewButtons, ProofViewButton } from "@/components/finance/proof-actions";

export const metadata: Metadata = { title: "To verify" };

const PAGE_SIZE = 25;
const tabs: { key: ProofStatus | "all"; label: string }[] = [
  { key: "Pending", label: "To verify" },
  { key: "Confirmed", label: "Confirmed" },
  { key: "Rejected", label: "Rejected" },
  { key: "all", label: "All" },
];
const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));
const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E10.8: proofs of bank payment from parents, oldest first. Confirm records the payment; reject emails the reason. */
export default async function ProofsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="To verify" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const requested = one(sp.status);
  const tab = tabs.find((t) => t.key === requested)?.key ?? "Pending";
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const query = tab === "all" ? { all: true, page, pageSize: PAGE_SIZE } : tab === "Pending" ? { page, pageSize: PAGE_SIZE } : { status: tab, page, pageSize: PAGE_SIZE };
  const proofs = await load((api) => api.GET("/api/finance/proofs", { params: { query } }));
  const today = schoolToday();
  const href = (key: string, p = 1) => {
    const q = new URLSearchParams();
    if (key !== "Pending") q.set("status", key);
    if (p > 1) q.set("page", String(p));
    const qs = q.toString();
    return qs ? `/admin/proofs?${qs}` : "/admin/proofs";
  };

  return (
    <>
      <PageHeader
        title="To verify"
        description="Bank transfers parents told us about. Check each one reached the school's account, then confirm (a receipt is emailed) or reject with a reason."
      />
      <nav aria-label="Proof status" className="mb-5 flex flex-wrap gap-1 rounded-input bg-surface-2 p-1 sm:inline-flex">
        {tabs.map((t) => (
          <Link
            key={t.key}
            href={href(t.key)}
            aria-current={tab === t.key ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", tab === t.key ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
          >
            {t.label}
          </Link>
        ))}
      </nav>
      <Card className="overflow-hidden">
        {!proofs.ok ? (
          <ProblemState problem={proofs.problem} />
        ) : proofs.data.items.length === 0 ? (
          <EmptyState icon={Landmark} title={tab === "Pending" ? "Nothing to verify" : "No proofs here"}>
            {tab === "Pending" ? "Proofs parents send from the portal appear here, oldest first." : "Try another tab."}
          </EmptyState>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[64rem] text-sm">
                <thead className="text-left text-xs uppercase tracking-wider text-muted">
                  <tr className="border-b border-border">
                    <th className="px-4 py-2.5 font-medium">Student</th>
                    <th className="px-3 py-2.5 font-medium">Invoice</th>
                    <th className="px-3 py-2.5 text-right font-medium">Amount</th>
                    <th className="px-3 py-2.5 font-medium">Paid on</th>
                    <th className="px-3 py-2.5 font-medium">Bank ref.</th>
                    <th className="px-3 py-2.5 font-medium">Sent</th>
                    <th className="px-3 py-2.5 text-right font-medium">Balance</th>
                    <th className="px-4 py-2.5 font-medium">
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {proofs.data.items.map((p) => (
                    <tr key={p.id} className="align-top">
                      <td className="px-4 py-2.5">
                        <span className="font-medium">{p.studentName}</span>
                        <span className="tabular block text-xs text-muted">{p.admissionNumber}</span>
                      </td>
                      <td className="px-3 py-2.5">
                        <Link href={`/admin/invoices/${p.invoiceId}`} className="tabular text-brand hover:underline">
                          {p.invoiceNumber}
                        </Link>
                      </td>
                      <td className="tabular px-3 py-2.5 text-right font-medium">{formatNaira(p.amount)}</td>
                      <td className="px-3 py-2.5 whitespace-nowrap">{day(p.paidOn)}</td>
                      <td className="px-3 py-2.5 break-all">{p.bankReference ?? <span className="text-muted">—</span>}</td>
                      <td className="px-3 py-2.5 whitespace-nowrap text-muted">{timeFormat.format(new Date(p.submittedAt))}</td>
                      <td className="tabular px-3 py-2.5 text-right">{formatNaira(p.invoiceBalance)}</td>
                      <td className="px-4 py-2.5">
                        <div className="flex flex-wrap items-center gap-1.5">
                          <ProofViewButton proof={p} />
                          {p.status === "Pending" ? (
                            <ProofReviewButtons proof={p} today={today} />
                          ) : (
                            <span className="flex flex-col">
                              <Badge tone={proofStatusTone[p.status]}>{p.status}</Badge>
                              {p.receiptNumber && <span className="tabular mt-1 text-xs text-muted">{p.receiptNumber}</span>}
                              {p.rejectReason && <span className="mt-1 max-w-56 text-xs text-muted">{p.rejectReason}</span>}
                            </span>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-border px-4 py-3">
              <Pagination page={proofs.data.page} pageSize={proofs.data.pageSize} totalCount={proofs.data.totalCount} hrefFor={(p) => href(tab, p)} noun="proofs" />
            </div>
          </>
        )}
      </Card>
    </>
  );
}
