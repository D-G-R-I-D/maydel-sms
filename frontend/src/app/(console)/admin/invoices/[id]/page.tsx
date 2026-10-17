import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { formatNaira, invoiceStatusLabel, invoiceStatusTone } from "@/lib/finance/rules";
import { scheduleStates, signed, type ScheduleState } from "@/lib/finance/payments";
import { schoolToday } from "@/lib/validation/students";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, Alert, ProblemState } from "@/components/ui/states";
import { InvoiceAdminActions, PaymentsTable, RecordPaymentButton } from "@/components/finance/invoice-actions";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import { proofStatusLabel, proofStatusTone } from "@/lib/finance/proofs";

const stateBadge: Record<ScheduleState, { label: string; tone: "success" | "danger" | "warning" | "neutral" }> = {
  paid: { label: "Paid", tone: "success" },
  overdue: { label: "Overdue", tone: "danger" },
  due: { label: "Due now", tone: "warning" },
  upcoming: { label: "Upcoming", tone: "neutral" },
};

export const metadata: Metadata = { title: "Invoice" };

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));

/** E10: one invoice: lines (discounts negative), totals, the instalment schedule, payments and (admin) corrections. */
export default async function InvoicePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/invoices" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Invoices
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        {back}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  if (!GUID.test(id)) notFound();
  const [invoice, invoiceProofs] = await Promise.all([
    load((api) => api.GET("/api/finance/invoices/{id}", { params: { path: { id } } })),
    // Proofs of bank payment for this invoice, every status (E10.8).
    load((api) => api.GET("/api/finance/proofs", { params: { query: { invoiceId: id, page: 1, pageSize: 50 } } })),
  ]);
  const proofs = invoiceProofs.ok ? invoiceProofs.data.items : [];
  if (!invoice.ok) {
    if (invoice.problem.status === 404) notFound();
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={invoice.problem} />
        </Card>
      </>
    );
  }
  const inv = invoice.data;
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const today = schoolToday();
  const cancelled = inv.status === "Cancelled";
  const states = scheduleStates(inv.schedule, inv.paid, today);

  return (
    <>
      {back}
      <PageHeader
        title={inv.number}
        description={
          <>
            {inv.studentName} · <span className="tabular">{inv.admissionNumber}</span>
            {inv.classRoom ? ` · ${inv.classRoom}` : ""} · {inv.term} {inv.session}
          </>
        }
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={invoiceStatusTone[inv.status]}>{invoiceStatusLabel[inv.status]}</Badge>
            <PdfDownloadButton href={`/api/media/finance/invoices/${inv.id}/pdf`} label="Invoice PDF" fallbackName={`${inv.number} - ${inv.studentName}.pdf`} size="sm" />
            {!cancelled && <RecordPaymentButton invoice={inv} today={today} />}
          </div>
        }
      />
      {!cancelled && !inv.isPublished && (
        <Alert tone="warning" className="mb-5" title="Draft: not visible to parents yet">
          Publish invoices from the Invoices page when the term&apos;s bills are ready. Parents then see it in the portal and can pay online.
        </Alert>
      )}
      {cancelled && (
        <Alert tone="warning" className="mb-5" title="Cancelled">
          {inv.cancelReason ?? "This invoice was cancelled."} Generate invoices again to bill this student for the term.
        </Alert>
      )}
      {!cancelled && inv.overdue > 0 && (
        <Alert tone="danger" className="mb-5" title={`${formatNaira(inv.overdue)} overdue`}>
          {formatNaira(inv.dueNow)} should have been paid by today; {formatNaira(inv.paid)} has been.
        </Alert>
      )}
      <div className="grid gap-5 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <Card className="overflow-hidden">
          <h2 className="border-b border-border px-5 py-3 font-display text-lg font-semibold">Charges</h2>
          <table className="w-full text-sm">
            <tbody className="divide-y divide-border">
              {inv.lines.map((l, i) => (
                <tr key={i}>
                  <td className="px-5 py-2.5">{l.description}</td>
                  <td className={cn("tabular px-5 py-2.5 text-right", l.amount < 0 && "text-success")}>{signed(l.amount, formatNaira)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot className="border-t-2 border-border">
              <tr>
                <th className="px-5 py-2.5 text-left font-semibold">Total</th>
                <td className="tabular px-5 py-2.5 text-right font-semibold">{formatNaira(inv.total)}</td>
              </tr>
              <tr>
                <th className="px-5 py-1.5 text-left font-normal text-muted">Paid</th>
                <td className="tabular px-5 py-1.5 text-right text-muted">{formatNaira(inv.paid)}</td>
              </tr>
              <tr>
                <th className="px-5 pt-1.5 pb-3 text-left font-semibold">Balance</th>
                <td className={cn("tabular px-5 pt-1.5 pb-3 text-right font-semibold", inv.balance > 0 ? "text-danger" : "text-success")}>{formatNaira(inv.balance)}</td>
              </tr>
            </tfoot>
          </table>
        </Card>

        <Card className="h-fit overflow-hidden">
          <h2 className="border-b border-border px-5 py-3 font-display text-lg font-semibold">Payment schedule</h2>
          {inv.schedule.length === 0 ? (
            <p className="px-5 py-4 text-sm">
              Pay in full{inv.dueOn ? <> by <span className="font-medium">{day(inv.dueOn)}</span></> : ""}.
            </p>
          ) : (
            <table className="w-full text-sm">
              <thead className="text-left text-xs uppercase tracking-wider text-muted">
                <tr>
                  <th className="px-5 py-2 font-medium">Part</th>
                  <th className="px-3 py-2 font-medium">Due</th>
                  <th className="px-3 py-2 text-right font-medium">Amount</th>
                  <th className="px-5 py-2 font-medium">
                    <span className="sr-only">Status</span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {inv.schedule.map((s, i) => (
                  <tr key={s.order}>
                    <td className="px-5 py-2.5">
                      {s.order} <span className="text-muted">({s.percent}%)</span>
                    </td>
                    <td className="px-3 py-2.5">{day(s.dueOn)}</td>
                    <td className="tabular px-3 py-2.5 text-right">{formatNaira(s.amount)}</td>
                    <td className="px-5 py-2.5">{!cancelled && <Badge tone={stateBadge[states[i]].tone}>{stateBadge[states[i]].label}</Badge>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <p className="border-t border-border px-5 py-3 text-xs text-muted">
            Issued {day(inv.issuedAt)}. Use admission number <span className="tabular font-medium text-text">{inv.admissionNumber}</span> as the bank-transfer reference.
          </p>
        </Card>
      </div>

      <Card className="mt-5 overflow-hidden">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-5 py-3">
          <h2 className="font-display text-lg font-semibold">Payments</h2>
          {isAdmin && !cancelled && <InvoiceAdminActions invoice={inv} />}
        </div>
        <PaymentsTable payments={inv.payments} isAdmin={isAdmin} />
      </Card>

      {proofs.length > 0 && (
        <Card className="mt-5 overflow-hidden">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border px-5 py-3">
            <h2 className="font-display text-lg font-semibold">Proofs of payment</h2>
            <Link href="/admin/proofs" className="text-sm text-brand hover:underline">
              To verify
            </Link>
          </div>
          <ul className="divide-y divide-border text-sm">
            {proofs.map((p) => (
              <li key={p.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-5 py-2.5">
                <span className="tabular min-w-0 flex-1">
                  {formatNaira(p.amount)} <span className="text-muted">paid {day(p.paidOn)}</span>
                  {p.bankReference && <span className="text-muted"> · ref {p.bankReference}</span>}
                </span>
                <Badge tone={proofStatusTone[p.status]}>{proofStatusLabel[p.status]}</Badge>
                {p.receiptNumber && <span className="tabular text-xs text-muted">{p.receiptNumber}</span>}
                {p.rejectReason && <span className="w-full text-xs text-muted">Not accepted: {p.rejectReason}</span>}
              </li>
            ))}
          </ul>
        </Card>
      )}
    </>
  );
}
