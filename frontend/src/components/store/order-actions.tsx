"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Banknote, CheckCheck, PackageCheck, XCircle } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { Loaded } from "@/lib/api/server";
import { runAction } from "@/lib/client/action";
import { formatNaira } from "@/lib/finance/rules";
import { cancelStoreOrder, markOrderCollected, markOrderReady, payOrder } from "@/lib/store/actions";
import { officeMethods, type PayMethod, type StoreOrder } from "@/lib/store/store";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, Select } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { PdfDownloadButton } from "@/components/reports/pdf-download";

/** Take payment for an unpaid order: cash, transfer, POS, or onto the term's fees. */
function PayDialog({ order, onDone }: { order: StoreOrder; onDone: () => void }) {
  const [method, setMethod] = useState<PayMethod>("Cash");
  const [reference, setReference] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const id = `pay-${order.id}`;
  return (
    <FormDialog
      title={`Take payment · ${order.number}`}
      description={`${order.student} · ${formatNaira(order.total)}`}
      onOpenChange={(v) => v && (setMethod("Cash"), setReference(""), setProblem(null))}
      trigger={
        <Button size="sm">
          <Banknote className="size-4" aria-hidden />
          Take payment
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4">
          {problem && <Alert tone="danger">{problemMessage(problem)}</Alert>}
          <Field id={`${id}-m`} label="Paid with">
            <Select id={`${id}-m`} value={method ?? "Cash"} onChange={(e) => setMethod(e.target.value as PayMethod)}>
              {officeMethods.map((m) => (
                <option key={m.value} value={m.value ?? ""}>
                  {m.label}
                </option>
              ))}
            </Select>
          </Field>
          {method === "Fees" ? (
            <p className="text-sm text-muted">Adds {formatNaira(order.total)} to {order.student}&apos;s invoice for the current term.</p>
          ) : (
            <Field id={`${id}-r`} label="Reference (optional)" hint="Transfer or POS reference, receipt number…">
              <Input id={`${id}-r`} aria-describedby={`${id}-r-hint`} value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} />
            </Field>
          )}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              onClick={async () => {
                setBusy(true);
                const r = await runAction(() => payOrder(order.id, method, method === "Fees" ? null : reference.trim() || null), {
                  success: method === "Fees" ? "Added to fees" : "Payment recorded",
                  onProblem: setProblem,
                });
                setBusy(false);
                if (r) {
                  close();
                  onDone();
                }
              }}
            >
              {method === "Fees" ? "Add to fees" : "Record payment"}
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

/** Next steps for one order: Placed → Ready → Collected, payment any time before collection, cancel before it's paid. */
export function OrderActions({ order }: { order: StoreOrder }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [reason, setReason] = useState("");
  const refresh = () => startTransition(() => router.refresh());
  const open = order.status === "Placed" || order.status === "Ready";
  const unpaid = order.payment === "Unpaid";

  const step = async (action: () => Promise<Loaded<StoreOrder>>, success: string) => {
    const r = await runAction(action, { success });
    if (r) refresh();
    return Boolean(r);
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      {open && unpaid && <PayDialog order={order} onDone={refresh} />}
      {order.status === "Placed" && (
        <ConfirmDialog
          title={`Mark ${order.number} ready?`}
          confirmLabel="Mark ready"
          pendingLabel="Saving…"
          onConfirm={() => step(() => markOrderReady(order.id), "Marked ready")}
          trigger={
            <Button size="sm" variant={unpaid ? "secondary" : "primary"}>
              <PackageCheck className="size-4" aria-hidden />
              Ready
            </Button>
          }
        >
          {order.fromPortal ? "The parent gets a notification that it's ready to collect." : "It's packed and waiting at the office."}
        </ConfirmDialog>
      )}
      {open && (
        <ConfirmDialog
          title={`Hand over ${order.number}?`}
          confirmLabel="Collected"
          pendingLabel="Saving…"
          onConfirm={() => step(() => markOrderCollected(order.id), "Marked collected")}
          trigger={
            <Button size="sm" variant="secondary" disabled={unpaid} title={unpaid ? "Take payment first" : undefined}>
              <CheckCheck className="size-4" aria-hidden />
              Collected
            </Button>
          }
        >
          Confirm the items were handed to {order.student} or their parent.
        </ConfirmDialog>
      )}
      {open && unpaid && (
        <ConfirmDialog
          title={`Cancel ${order.number}?`}
          confirmLabel="Cancel order"
          cancelLabel="Keep it"
          pendingLabel="Cancelling…"
          tone="destructive"
          onOpenChange={(v) => v && setReason("")}
          onConfirm={() => step(() => cancelStoreOrder(order.id, reason.trim() || null), "Order cancelled")}
          trigger={
            <Button size="sm" variant="ghost">
              <XCircle className="size-4" aria-hidden />
              Cancel
            </Button>
          }
        >
          <span className="block">The held stock goes back on the shelf.</span>
          <label htmlFor={`cancel-${order.id}`} className="mt-3 block font-medium text-text">
            Reason (optional)
          </label>
          <Input id={`cancel-${order.id}`} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} className="mt-1.5" />
        </ConfirmDialog>
      )}
      <PdfDownloadButton href={`/api/media/store/orders/${order.id}/receipt`} fallbackName={`receipt-${order.number}.pdf`} label="Receipt" size="sm" showView={false} />
    </div>
  );
}
