import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Check, ChevronLeft, Circle, XCircle } from "lucide-react";
import { load } from "@/lib/api/server";
import { GUID } from "@/lib/media/binary-proxy";
import { formatNaira } from "@/lib/finance/rules";
import { formatLagos } from "@/lib/time/lagos";
import { payMethodLabel, paymentLabel, statusLabel, statusTone, type StoreOrder } from "@/lib/store/store";
import { CancelShopOrder } from "@/components/store/cancel-shop-order";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Shop order" };

function timeline(o: StoreOrder) {
  if (o.status === "Cancelled")
    return [
      { label: "Ordered", at: o.createdAt, done: true },
      { label: "Cancelled", at: o.cancelledAt, done: true, cancelled: true, note: o.cancelReason },
    ];
  return [
    { label: "Ordered", at: o.createdAt, done: true },
    { label: "Ready to collect", at: o.readyAt, done: o.status !== "Placed" },
    { label: "Collected", at: o.collectedAt, done: o.status === "Collected" },
  ];
}

/** E19 portal: one order (the `StoreOrderReady` notification links here by order id); other families get 404. */
export default async function ShopOrderPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) notFound();
  const back = (
    <Link href="/portal/shop" className="mb-3 inline-flex items-center gap-1 text-sm text-muted hover:text-text">
      <ChevronLeft className="size-4" aria-hidden />
      School shop
    </Link>
  );
  const r = await load((api) => api.GET("/api/portal/shop/orders/{orderId}", { params: { path: { orderId: id } } }));
  if (!r.ok && r.problem.status === 404) notFound();
  if (!r.ok)
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={r.problem} />
        </Card>
      </>
    );
  const order = r.data;
  const steps = timeline(order);

  return (
    <>
      {back}
      <PageHeader title={`Order ${order.number}`} description={`For ${order.student} · placed ${formatLagos(order.createdAt)}`} />
      <div className="grid gap-5 lg:grid-cols-[1fr_20rem] lg:items-start">
        <Card className="p-5">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={statusTone[order.status]}>{statusLabel[order.status]}</Badge>
            <Badge tone={order.payment === "Unpaid" ? "warning" : "success"}>{paymentLabel[order.payment]}</Badge>
          </div>
          {order.status === "Ready" && (
            <p className="mt-3 rounded-input bg-success/10 p-3 text-sm text-success">
              It&apos;s ready. Collect it from the school office{order.payment === "Unpaid" ? " and pay there" : ""}.
            </p>
          )}
          <ul className="mt-4 divide-y divide-border text-sm">
            {order.lines.map((l) => (
              <li key={l.variantId} className="flex items-center gap-3 py-2">
                <span className="min-w-0 flex-1">
                  <span className="font-medium">{l.item}</span> <span className="text-muted">· {l.variant}</span>
                </span>
                <span className="tabular text-muted">
                  {l.quantity} × {formatNaira(l.unitPrice)}
                </span>
                <span className="tabular w-24 text-right">{formatNaira(l.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <div className="flex items-center justify-between border-t border-border pt-2 font-semibold">
            <span>Total</span>
            <span className="tabular">{formatNaira(order.total)}</span>
          </div>
          <p className="mt-3 text-sm text-muted">
            {order.payment === "ChargedToFees"
              ? `Added to school fees${order.invoiceNumber ? ` (invoice ${order.invoiceNumber})` : ""}.`
              : order.payment === "Paid"
                ? `Paid${order.paidWith ? ` by ${payMethodLabel(order.paidWith).toLowerCase()}` : ""}${order.paidAt ? ` on ${formatLagos(order.paidAt)}` : ""}.`
                : order.status === "Cancelled"
                  ? "Nothing to pay."
                  : `Pay at the office when you collect.${order.holdUntil ? ` Held until ${formatLagos(order.holdUntil)}.` : ""}`}
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            <PdfDownloadButton href={`/api/media/portal/children/${order.studentId}/shop/orders/${order.id}/receipt`} fallbackName={`receipt-${order.number}.pdf`} label="Receipt" size="sm" />
            {order.status === "Placed" && <CancelShopOrder studentId={order.studentId} orderId={order.id} number={order.number} />}
          </div>
        </Card>

        <Card className="p-5">
          <h2 className="text-sm font-semibold">Progress</h2>
          <ol className="mt-3 space-y-0">
            {steps.map((s, i) => {
              const Icon = "cancelled" in s && s.cancelled ? XCircle : s.done ? Check : Circle;
              return (
                <li key={s.label} className="relative flex gap-3 pb-5 last:pb-0">
                  {i < steps.length - 1 && <span className={cn("absolute top-7 left-3.5 h-[calc(100%-1.75rem)] w-px", steps[i + 1].done ? "bg-brand" : "bg-border")} aria-hidden />}
                  <span
                    className={cn(
                      "flex size-7 shrink-0 items-center justify-center rounded-full",
                      "cancelled" in s && s.cancelled ? "bg-danger/12 text-danger" : s.done ? "bg-brand text-on-brand" : "border border-border text-muted",
                    )}
                  >
                    <Icon className="size-4" aria-hidden />
                  </span>
                  <div className="text-sm">
                    <p className={cn("font-medium", !s.done && "text-muted")}>{s.label}</p>
                    {s.at && <p className="text-xs text-muted">{formatLagos(s.at)}</p>}
                    {"note" in s && s.note && <p className="text-xs text-muted">{s.note}</p>}
                    <span className="sr-only">{s.done ? "done" : "not yet"}</span>
                  </div>
                </li>
              );
            })}
          </ol>
        </Card>
      </div>
    </>
  );
}
