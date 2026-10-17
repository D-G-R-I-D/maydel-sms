import type { Metadata } from "next";
import Link from "next/link";
import { CreditCard } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import type { OnlinePaymentStatus } from "@/lib/api/types";
import { formatNaira } from "@/lib/finance/rules";
import { onlineStatusLabel, onlineStatusTone } from "@/lib/finance/payments";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";

export const metadata: Metadata = { title: "Online payments" };

const STATUSES: OnlinePaymentStatus[] = ["NeedsReview", "Pending", "Succeeded", "Failed"];
const PAGE_SIZE = 25;
const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });
const when = (iso: string | null) => (iso ? timeFormat.format(new Date(iso)) : "—");
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E10.5: Paystack payments for staff, with "Needs review" (amount mismatch or excess) first. */
export default async function OnlinePaymentsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Online payments" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const requested = one(sp.status);
  const status = STATUSES.includes(requested as OnlinePaymentStatus) ? (requested as OnlinePaymentStatus) : undefined;
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const [list, review] = await Promise.all([
    load((api) => api.GET("/api/finance/online-payments", { params: { query: { status, page, pageSize: PAGE_SIZE } } })),
    load((api) => api.GET("/api/finance/online-payments", { params: { query: { status: "NeedsReview", page: 1, pageSize: 1 } } })),
  ]);
  const reviewCount = review.ok ? review.data.totalCount : 0;
  const href = (s: OnlinePaymentStatus | undefined, p = 1) => {
    const q = new URLSearchParams();
    if (s) q.set("status", s);
    if (p > 1) q.set("page", String(p));
    const qs = q.toString();
    return qs ? `/admin/payments?${qs}` : "/admin/payments";
  };

  return (
    <>
      <PageHeader title="Online payments" description="Payments parents made with Paystack. Each one is checked with Paystack before it's recorded on the invoice." />
      <nav aria-label="Filter by status" className="mb-5 flex flex-wrap gap-1 rounded-input bg-surface-2 p-1 sm:inline-flex">
        {[undefined, ...STATUSES].map((s) => (
          <Link
            key={s ?? "all"}
            href={href(s)}
            aria-current={status === s ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center gap-1.5 rounded-[6px] px-3 text-sm transition-colors", status === s ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
          >
            {s ? onlineStatusLabel[s] : "All"}
            {s === "NeedsReview" && reviewCount > 0 && <Badge tone="warning">{reviewCount}</Badge>}
          </Link>
        ))}
      </nav>
      <Card className="overflow-hidden">
        {!list.ok ? (
          <ProblemState problem={list.problem} />
        ) : list.data.items.length === 0 ? (
          <EmptyState icon={CreditCard} title={status === "NeedsReview" ? "Nothing to review" : "No online payments yet"}>
            {status === "NeedsReview" ? "Payments with a wrong amount or more than was owed appear here." : "Payments parents make with “Pay online” appear here."}
          </EmptyState>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[60rem] text-sm">
                <thead className="text-left text-xs uppercase tracking-wider text-muted">
                  <tr className="border-b border-border">
                    <th className="px-4 py-2.5 font-medium">Reference</th>
                    <th className="px-3 py-2.5 font-medium">Invoice</th>
                    <th className="px-3 py-2.5 font-medium">Admission no.</th>
                    <th className="px-3 py-2.5 font-medium">Student</th>
                    <th className="px-3 py-2.5 text-right font-medium">Amount</th>
                    <th className="px-3 py-2.5 text-right font-medium">Excess</th>
                    <th className="px-3 py-2.5 font-medium">Status</th>
                    <th className="px-3 py-2.5 font-medium">Started</th>
                    <th className="px-4 py-2.5 font-medium">Completed</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {list.data.items.map((p) => (
                    <tr key={p.id} className="align-top">
                      <td className="px-4 py-2.5">
                        <span className="tabular font-mono text-xs break-all">{p.reference}</span>
                        {p.receiptNumber && <span className="tabular block text-xs text-muted">{p.receiptNumber}</span>}
                      </td>
                      <td className="px-3 py-2.5">
                        <Link href={`/admin/invoices/${p.invoiceId}`} className="tabular text-brand hover:underline">
                          {p.invoiceNumber}
                        </Link>
                      </td>
                      <td className="tabular px-3 py-2.5">{p.admissionNumber}</td>
                      <td className="px-3 py-2.5">{p.studentName}</td>
                      <td className="tabular px-3 py-2.5 text-right">{formatNaira(p.amount)}</td>
                      <td className={cn("tabular px-3 py-2.5 text-right", p.excess > 0 ? "font-medium text-warning" : "text-muted")}>{p.excess > 0 ? formatNaira(p.excess) : "—"}</td>
                      <td className="px-3 py-2.5">
                        <Badge tone={onlineStatusTone[p.status]}>{onlineStatusLabel[p.status]}</Badge>
                        {p.message && <span className="mt-1 block max-w-56 text-xs text-muted">{p.message}</span>}
                      </td>
                      <td className="px-3 py-2.5 whitespace-nowrap text-muted">{when(p.startedAt)}</td>
                      <td className="px-4 py-2.5 whitespace-nowrap text-muted">{when(p.completedAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-border px-4 py-3">
              <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} hrefFor={(p) => href(status, p)} noun="payments" />
            </div>
          </>
        )}
      </Card>
    </>
  );
}
