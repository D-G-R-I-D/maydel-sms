import type { Metadata } from "next";
import Link from "next/link";
import { ShoppingBag } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatNaira } from "@/lib/finance/rules";
import { formatLagos } from "@/lib/time/lagos";
import { payMethodLabel, paymentLabel, statusLabel, statusTone, type OrderStatus } from "@/lib/store/store";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { StoreTabs } from "@/components/store/store-tabs";
import { OrderActions } from "@/components/store/order-actions";

export const metadata: Metadata = { title: "Store orders" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;
const filters: { key: OrderStatus | "all"; label: string }[] = [
  { key: "Placed", label: "New" },
  { key: "Ready", label: "Ready to collect" },
  { key: "Collected", label: "Collected" },
  { key: "Cancelled", label: "Cancelled" },
  { key: "all", label: "All" },
];
const PAGE = 20;

/** E19: the orders queue. Parents' orders arrive as New; pack them, mark Ready (parent notified), take payment, hand over. */
export default async function StoreOrdersPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Store orders" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const s = one(sp.status);
  const status = filters.find((f) => f.key === s)?.key ?? "Placed";
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const orders = await load((api) => api.GET("/api/store/orders", { params: { query: { ...(status !== "all" ? { status } : {}), page, pageSize: PAGE } } }));
  const href = (st: string, p = 1) => `/admin/store/orders?status=${st}${p > 1 ? `&page=${p}` : ""}`;

  return (
    <>
      <PageHeader title="School store" description="Parents' orders and sales waiting to be collected." />
      <StoreTabs active="orders" />
      <nav aria-label="Order status" className="mb-4 flex flex-wrap gap-2">
        {filters.map((f) => (
          <Link
            key={f.key}
            href={href(f.key)}
            aria-current={status === f.key ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center rounded-full border px-3 text-sm", status === f.key ? "border-brand bg-brand/10 font-medium text-brand" : "border-border text-muted hover:text-text")}
          >
            {f.label}
          </Link>
        ))}
      </nav>

      {!orders.ok ? (
        <Card>
          <ProblemState problem={orders.problem} />
        </Card>
      ) : orders.data.items.length === 0 ? (
        <Card>
          <EmptyState icon={ShoppingBag} title={status === "Placed" ? "No new orders" : "No orders here"}>
            {status === "Placed" ? "Orders parents place in the portal show up here." : "Try another tab."}
          </EmptyState>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <ul className="divide-y divide-border">
            {orders.data.items.map((o) => (
              <li key={o.id} className="space-y-3 p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="font-medium">
                      <span className="tabular">{o.number}</span> · {o.student} <span className="text-sm font-normal text-muted">({o.admissionNumber})</span>
                    </p>
                    <p className="text-xs text-muted">
                      {o.fromPortal ? `Ordered by ${o.placedBy} in the portal` : `Sold by ${o.placedBy}`} · {formatLagos(o.createdAt)}
                      {o.holdUntil && o.payment === "Unpaid" && (o.status === "Placed" || o.status === "Ready") ? ` · held until ${formatLagos(o.holdUntil)}` : ""}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-1.5">
                    <Badge tone={statusTone[o.status]}>{statusLabel[o.status]}</Badge>
                    <Badge tone={o.payment === "Unpaid" ? "warning" : "success"}>
                      {paymentLabel[o.payment]}
                      {o.payment === "Paid" && o.paidWith ? ` · ${payMethodLabel(o.paidWith)}` : ""}
                    </Badge>
                    <span className="tabular ml-1 font-semibold">{formatNaira(o.total)}</span>
                  </div>
                </div>
                <ul className="text-sm text-muted">
                  {o.lines.map((l) => (
                    <li key={l.variantId}>
                      {l.quantity} × {l.item} ({l.variant})
                    </li>
                  ))}
                </ul>
                {o.status === "Cancelled" && o.cancelReason && <p className="text-sm text-muted">Cancelled: {o.cancelReason}</p>}
                {o.payment === "ChargedToFees" && o.invoiceNumber && <p className="text-xs text-muted">On invoice {o.invoiceNumber}</p>}
                <OrderActions order={o} />
              </li>
            ))}
          </ul>
          <Pagination page={orders.data.page} pageSize={orders.data.pageSize} totalCount={orders.data.totalCount} hrefFor={(p) => href(status, p)} noun="orders" />
        </Card>
      )}
    </>
  );
}
