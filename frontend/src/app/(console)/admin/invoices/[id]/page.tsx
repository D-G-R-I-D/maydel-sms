import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { formatNaira, invoiceStatusLabel, invoiceStatusTone } from "@/lib/finance/rules";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Invoice" };

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));

/** E10 part 1: one invoice: lines (discounts negative), totals and the instalment schedule. */
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
  const invoice = await load((api) => api.GET("/api/finance/invoices/{id}", { params: { path: { id } } }));
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
        actions={<Badge tone={invoiceStatusTone[inv.status]}>{invoiceStatusLabel[inv.status]}</Badge>}
      />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <Card className="overflow-hidden">
          <h2 className="border-b border-border px-5 py-3 font-display text-lg font-semibold">Charges</h2>
          <table className="w-full text-sm">
            <tbody className="divide-y divide-border">
              {inv.lines.map((l, i) => (
                <tr key={i}>
                  <td className="px-5 py-2.5">{l.description}</td>
                  <td className={cn("tabular px-5 py-2.5 text-right", l.amount < 0 && "text-success")}>{l.amount < 0 ? `−${formatNaira(-l.amount)}` : formatNaira(l.amount)}</td>
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
                  <th className="px-5 py-2 text-right font-medium">Amount</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {inv.schedule.map((s) => (
                  <tr key={s.order}>
                    <td className="px-5 py-2.5">
                      {s.order} <span className="text-muted">({s.percent}%)</span>
                    </td>
                    <td className="px-3 py-2.5">{day(s.dueOn)}</td>
                    <td className="tabular px-5 py-2.5 text-right">{formatNaira(s.amount)}</td>
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
    </>
  );
}
