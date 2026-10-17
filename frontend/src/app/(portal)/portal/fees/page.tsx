import type { Metadata } from "next";
import { ChevronDown, Info, ReceiptText, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import type { InvoiceDetail } from "@/lib/api/types";
import { formatNaira, invoiceStatusLabel, invoiceStatusTone } from "@/lib/finance/rules";
import { methodLabel, nextDueDate, scheduleStates, signed, type ScheduleState } from "@/lib/finance/payments";
import { schoolToday } from "@/lib/validation/students";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { PhotoAvatar } from "@/components/media/photo-avatar";
import { ChildSwitcher } from "@/components/portal/child-switcher";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import { PayOnlineButton } from "@/components/portal/pay-online";

export const metadata: Metadata = { title: "Fees" };

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));
const stateBadge: Record<ScheduleState, { label: string; tone: "success" | "danger" | "warning" | "neutral" }> = {
  paid: { label: "Paid", tone: "success" },
  overdue: { label: "Overdue", tone: "danger" },
  due: { label: "Due today", tone: "warning" },
  upcoming: { label: "Upcoming", tone: "neutral" },
};

function InvoiceCard({ studentId, inv, today, open }: { studentId: string; inv: InvoiceDetail; today: string; open: boolean }) {
  const states = scheduleStates(inv.schedule, inv.paid, today);
  return (
    <Card className="overflow-hidden">
      <details open={open} className="group">
        <summary className="flex cursor-pointer list-none flex-wrap items-center gap-x-4 gap-y-2 px-5 py-4 hover:bg-surface-2/40 [&::-webkit-details-marker]:hidden">
          <div className="min-w-0 flex-1 basis-48">
            <p className="font-medium">
              {inv.term} {inv.session}
            </p>
            <p className="tabular text-xs text-muted">{inv.number}</p>
          </div>
          <dl className="tabular grid grid-cols-3 gap-x-4 text-right text-sm">
            <dt className="text-xs text-muted">Total</dt>
            <dt className="text-xs text-muted">Paid</dt>
            <dt className="text-xs text-muted">Balance</dt>
            <dd>{formatNaira(inv.total)}</dd>
            <dd>{formatNaira(inv.paid)}</dd>
            <dd className={cn("font-semibold", inv.balance > 0 ? "text-danger" : "text-success")}>{formatNaira(inv.balance)}</dd>
          </dl>
          <Badge tone={invoiceStatusTone[inv.status]}>{invoiceStatusLabel[inv.status]}</Badge>
          <ChevronDown className="size-4 text-muted transition-transform group-open:rotate-180" aria-hidden />
        </summary>

        <div className="space-y-5 border-t border-border px-5 py-4">
          <section aria-label="Charges">
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted">Charges</h3>
            <table className="w-full text-sm">
              <tbody className="divide-y divide-border">
                {inv.lines.map((l, i) => (
                  <tr key={i}>
                    <td className="py-1.5 pr-3">{l.description}</td>
                    <td className={cn("tabular py-1.5 text-right", l.amount < 0 && "text-success")}>{signed(l.amount, formatNaira)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>

          <section aria-label="When to pay">
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted">When to pay</h3>
            {inv.schedule.length === 0 ? (
              <p className="text-sm">Pay in full{inv.dueOn ? ` by ${day(inv.dueOn)}` : ""}.</p>
            ) : (
              <ul className="divide-y divide-border text-sm">
                {inv.schedule.map((s, i) => (
                  <li key={s.order} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-1.5">
                    <span className="flex-1">
                      Part {s.order} ({s.percent}%) by {day(s.dueOn)}
                    </span>
                    <span className="tabular">{formatNaira(s.amount)}</span>
                    <Badge tone={stateBadge[states[i]].tone}>{stateBadge[states[i]].label}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section aria-label="Payments">
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted">Payments</h3>
            {inv.payments.length === 0 ? (
              <p className="text-sm text-muted">No payments yet.</p>
            ) : (
              <ul className="divide-y divide-border text-sm">
                {inv.payments.map((p) => (
                  <li key={p.id} className={cn("flex flex-wrap items-center gap-x-3 gap-y-1 py-2", p.isReversal && "text-danger")}>
                    <span className="min-w-0 flex-1">
                      <span className={cn("tabular font-medium", p.reversedBy && "line-through opacity-70")}>{p.receiptNumber}</span>
                      <span className="block text-xs text-muted">
                        {day(p.paidOn)} · {methodLabel[p.method]}
                        {p.isReversal && p.reversesReceipt ? ` · reverses ${p.reversesReceipt}` : ""}
                        {p.reversedBy ? ` · reversed by ${p.reversedBy}` : ""}
                      </span>
                    </span>
                    <span className={cn("tabular font-medium", p.reversedBy && "line-through opacity-70")}>{signed(p.amount, formatNaira)}</span>
                    <PdfDownloadButton
                      href={`/api/media/portal/children/${studentId}/receipts/${p.id}/pdf`}
                      label="Receipt"
                      fallbackName={`${p.receiptNumber}.pdf`}
                      variant="ghost"
                      size="sm"
                      showView={false}
                    />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <div className="flex flex-wrap items-center gap-2">
            {inv.balance > 0 && inv.status !== "Cancelled" && <PayOnlineButton studentId={studentId} invoice={inv} />}
            <PdfDownloadButton href={`/api/media/portal/children/${studentId}/invoices/${inv.id}/pdf`} label="Download invoice" fallbackName={`${inv.number}.pdf`} size="sm" />
          </div>
        </div>
      </details>
    </Card>
  );
}

/** E6.3: a child's fees for parents and students: balance, overdue, invoices with schedule and receipts. */
export default async function PortalFeesPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams;
  const requested = Array.isArray(sp.child) ? sp.child[0] : sp.child;
  const children = await load((api) => api.GET("/api/portal/children"));
  const header = <PageHeader title="Fees" description="What's been billed, what's been paid, and what's due next." />;

  if (!children.ok) {
    return (
      <>
        {header}
        <Card>
          <ProblemState problem={children.problem} />
        </Card>
      </>
    );
  }
  if (children.data.length === 0) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={Users} title="No children linked yet">
            Ask the school office to link your children to your account.
          </EmptyState>
        </Card>
      </>
    );
  }

  const child = children.data.find((c) => c.studentId === requested) ?? children.data[0];
  const fees = await load((api) => api.GET("/api/portal/children/{studentId}/fees", { params: { path: { studentId: child.studentId } } }));
  const today = schoolToday();
  const next = fees.ok ? nextDueDate(fees.data.invoices, today) : null;

  return (
    <>
      {header}
      <ChildSwitcher items={children.data} activeId={child.studentId} basePath="/portal/fees" />

      <Card className="mb-5 overflow-hidden">
        <div className="flex items-center gap-3 border-b border-border px-5 py-4">
          <PhotoAvatar kind="portal" id={child.studentId} name={child.name} photoVersion={child.photoVersion} className="size-11" />
          <div className="min-w-0">
            <h2 className="truncate font-display text-lg font-semibold">{child.name}</h2>
            <p className="text-sm text-muted">
              <span className="font-mono">{child.admissionNumber}</span>
              {child.classRoom ? ` · ${child.classRoom}` : ""}
            </p>
          </div>
        </div>
        {fees.ok && fees.data.invoices.length > 0 && (
          <dl className="grid gap-4 px-5 py-4 sm:grid-cols-3">
            <div>
              <dt className="text-sm text-muted">Balance</dt>
              <dd className="tabular font-display text-2xl font-semibold">{formatNaira(fees.data.balance)}</dd>
            </div>
            <div>
              <dt className="text-sm text-muted">Overdue</dt>
              <dd className={cn("tabular font-display text-2xl font-semibold", fees.data.overdue > 0 ? "text-danger" : "text-success")}>{formatNaira(fees.data.overdue)}</dd>
            </div>
            <div>
              <dt className="text-sm text-muted">Next due</dt>
              <dd className="font-display text-2xl font-semibold">{next ? day(next) : "Nothing due"}</dd>
            </div>
          </dl>
        )}
      </Card>

      {!fees.ok ? (
        <Card>
          <ProblemState problem={fees.problem} />
        </Card>
      ) : fees.data.invoices.length === 0 ? (
        <Card>
          <EmptyState icon={ReceiptText} title="No fees have been billed yet">
            Invoices for {child.name.split(" ")[0]} will appear here once the school issues them.
          </EmptyState>
        </Card>
      ) : (
        <div className="space-y-3">
          {fees.data.invoices.map((inv, i) => (
            <InvoiceCard key={inv.id} studentId={child.studentId} inv={inv} today={today} open={i === 0} />
          ))}
        </div>
      )}

      <Card className="mt-5 flex items-start gap-3 p-4 text-sm">
        <Info className="mt-0.5 size-5 shrink-0 text-brand" aria-hidden />
        <div>
          <p className="font-medium">How to pay</p>
          <p className="text-muted">
            Pay at the school&apos;s bursary (cash or POS) or by bank transfer. For transfers, use <span className="font-mono font-semibold text-text">{child.admissionNumber}</span> as the
            reference so the payment reaches {child.name.split(" ")[0]}&apos;s account. You&apos;ll get a receipt for every payment.
          </p>
        </div>
      </Card>
    </>
  );
}
