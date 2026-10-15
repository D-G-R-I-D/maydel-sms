"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Ban, CheckCircle2, Plus, Receipt as ReceiptIcon, RotateCcw, Wallet } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { InvoiceDetail, Payment, PaymentMethod, Receipt } from "@/lib/api/types";
import { addAdjustment, cancelInvoice, recordPayment, reversePayment } from "@/lib/finance/actions";
import { PAYMENT_METHODS, methodLabel, paymentErrors, signed } from "@/lib/finance/payments";
import { formatNaira, parseAmount } from "@/lib/finance/rules";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Field, Input, Select, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));
const showProblem = (title: string) => (p: Problem) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error(title, { description: p.errors ? Object.values(p.errors).flat().join(" ") : problemMessage(p) });

/**
 * Record payment → receipt panel. Amount prefilled with the balance; locked to it when instalments
 * aren't allowed. Stays mounted when the balance reaches 0 (only the trigger hides) so the receipt shows.
 */
export function RecordPaymentButton({ invoice, today }: { invoice: InvoiceDetail; today: string }) {
  const [open, setOpen] = useState(false);
  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const [amount, setAmount] = useState(invoice.balance.toFixed(2));
  const [method, setMethod] = useState<Exclude<PaymentMethod, "Online">>("Cash");
  const [reference, setReference] = useState("");
  const [paidOn, setPaidOn] = useState(today);
  const [touched, setTouched] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const errors = touched ? paymentErrors({ amount, paidOn, reference }, invoice, today) : {};

  const reset = () => {
    setReceipt(null);
    setAmount(invoice.balance.toFixed(2));
    setReference("");
    setPaidOn(today);
    setTouched(false);
    setProblem(null);
  };

  const save = async () => {
    setTouched(true);
    setProblem(null);
    if (Object.keys(paymentErrors({ amount, paidOn, reference }, invoice, today)).length > 0) return;
    setBusy(true);
    const r = await runAction(() => recordPayment(invoice.id, { amount: parseAmount(amount)!, method, reference: reference.trim() || null, paidOn: paidOn || null }), {
      success: (x) => `Payment recorded · ${x.receiptNumber}`,
      onProblem: setProblem,
    });
    setBusy(false);
    if (r) setReceipt(r); // the action's revalidation refreshes the page; this stays mounted to show the receipt
  };

  return (
    <FormDialog
      open={open}
      onOpenChange={(v) => {
        setOpen(v);
        if (!v) reset();
      }}
      title={receipt ? "Payment recorded" : "Record payment"}
      description={`${invoice.number} · ${invoice.studentName} · ${invoice.admissionNumber}`}
      trigger={
        invoice.balance > 0 ? (
          <Button>
            <Wallet className="size-4" aria-hidden />
            Record payment
          </Button>
        ) : undefined
      }
    >
      {(close) =>
        receipt ? (
          <div className="space-y-4">
            <div className="rounded-card border border-success/30 bg-success/8 p-4">
              <p className="flex items-center gap-2 font-medium text-success">
                <CheckCircle2 className="size-5" aria-hidden />
                Receipt {receipt.receiptNumber}
              </p>
              <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
                <dt className="text-muted">Admission no.</dt>
                <dd className="tabular font-semibold">{receipt.admissionNumber}</dd>
                <dt className="text-muted">Student</dt>
                <dd>
                  {receipt.studentName}
                  {receipt.classRoom ? ` · ${receipt.classRoom}` : ""}
                </dd>
                <dt className="text-muted">Amount</dt>
                <dd className="tabular font-semibold">{formatNaira(receipt.amount)}</dd>
                <dt className="text-muted">Method</dt>
                <dd>
                  {methodLabel[receipt.method]}
                  {receipt.reference ? ` · ${receipt.reference}` : ""}
                </dd>
                <dt className="text-muted">Paid on</dt>
                <dd>{day(receipt.paidOn)}</dd>
                <dt className="text-muted">Balance after</dt>
                <dd className={cn("tabular font-semibold", receipt.balanceAfter > 0 ? "text-danger" : "text-success")}>{formatNaira(receipt.balanceAfter)}</dd>
              </dl>
            </div>
            <DialogActions>
              <Button onClick={close}>Done</Button>
            </DialogActions>
          </div>
        ) : (
          <div className="space-y-4">
            {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
              <Alert tone="danger" title="Couldn't record the payment">
                {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
              </Alert>
            )}
            {!invoice.instalmentsAllowed && (
              <Alert tone="info" title="Full payment only this term">
                There&apos;s no instalment plan, so the whole balance of {formatNaira(invoice.balance)} is paid at once.
              </Alert>
            )}
            <div className="grid gap-4 sm:grid-cols-2">
              <Field id="pay-amount" label="Amount (₦)" error={errors.amount} hint={invoice.instalmentsAllowed && invoice.minimumPayment < invoice.balance ? `Minimum ${formatNaira(invoice.minimumPayment)} · balance ${formatNaira(invoice.balance)}` : `Balance ${formatNaira(invoice.balance)}`}>
                <Input {...describedBy("pay-amount", errors.amount, true)} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="tabular" readOnly={!invoice.instalmentsAllowed} />
              </Field>
              <Field id="pay-method" label="Method">
                <Select id="pay-method" value={method} onChange={(e) => setMethod(e.target.value as typeof method)}>
                  {PAYMENT_METHODS.map((m) => (
                    <option key={m.value} value={m.value}>
                      {m.label}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field id="pay-ref" label="Reference (optional)" error={errors.reference} hint="e.g. bank transfer or POS reference">
                <Input {...describedBy("pay-ref", errors.reference, true)} value={reference} onChange={(e) => setReference(e.target.value)} autoComplete="off" />
              </Field>
              <Field id="pay-date" label="Paid on" error={errors.paidOn}>
                <Input {...describedBy("pay-date", errors.paidOn)} type="date" value={paidOn} max={today} onChange={(e) => setPaidOn(e.target.value)} />
              </Field>
            </div>
            <DialogActions>
              <CancelButton />
              <Button onClick={save} loading={busy} loadingText="Recording…">
                <ReceiptIcon className="size-4" aria-hidden />
                Record payment
              </Button>
            </DialogActions>
          </div>
        )
      }
    </FormDialog>
  );
}

/** Payments and reversals. Reversals in red; reversed payments struck through. Admins can reverse once. */
export function PaymentsTable({ payments, isAdmin }: { payments: Payment[]; isAdmin: boolean }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [reversing, setReversing] = useState<Payment | null>(null);
  const [reason, setReason] = useState("");

  if (payments.length === 0) return <p className="px-5 py-4 text-sm text-muted">No payments yet.</p>;
  return (
    <>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[34rem] text-sm">
          <thead className="text-left text-xs uppercase tracking-wider text-muted">
            <tr>
              <th className="px-5 py-2 font-medium">Receipt</th>
              <th className="px-3 py-2 font-medium">Date</th>
              <th className="px-3 py-2 font-medium">Method</th>
              <th className="px-3 py-2 text-right font-medium">Amount</th>
              {isAdmin && <th className="w-24 px-3 py-2" aria-label="Actions" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {payments.map((p) => (
              <tr key={p.id} className={cn(p.isReversal && "text-danger")}>
                <td className="px-5 py-2.5">
                  <span className={cn("tabular font-medium", p.reversedBy && "line-through opacity-70")}>{p.receiptNumber}</span>
                  {p.isReversal && p.reversesReceipt && <span className="block text-xs">Reverses {p.reversesReceipt}</span>}
                  {p.reversedBy && <span className="block text-xs text-muted">Reversed by {p.reversedBy}</span>}
                  {p.note && <span className="block text-xs text-muted">{p.note}</span>}
                </td>
                <td className="px-3 py-2.5 whitespace-nowrap">{day(p.paidOn)}</td>
                <td className="px-3 py-2.5">
                  {methodLabel[p.method]}
                  {p.reference && <span className="block text-xs text-muted">{p.reference}</span>}
                </td>
                <td className={cn("tabular px-3 py-2.5 text-right font-medium", p.reversedBy && "line-through opacity-70")}>{signed(p.amount, formatNaira)}</td>
                {isAdmin && (
                  <td className="px-3 py-2.5 text-right">
                    {!p.isReversal && !p.reversedBy && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setReason("");
                          setReversing(p);
                        }}
                      >
                        <RotateCcw className="size-3.5" aria-hidden />
                        Reverse
                      </Button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ConfirmDialog
        open={reversing !== null}
        onOpenChange={(v) => !v && setReversing(null)}
        title={`Reverse ${reversing?.receiptNumber ?? "payment"}?`}
        confirmLabel="Reverse payment"
        pendingLabel="Reversing…"
        tone="destructive"
        onConfirm={async () => {
          if (!reversing) return true;
          if (!reason.trim()) {
            toast.error("Give a reason for the reversal.");
            return false;
          }
          const r = await runAction(() => reversePayment(reversing.id, reason.trim()), { success: (x) => `Reversed · ${x.receiptNumber}`, onProblem: showProblem("Couldn't reverse the payment") });
          if (r) startTransition(() => router.refresh());
          return r !== null;
        }}
      >
        <p>
          {reversing && `${formatNaira(reversing.amount)} goes back onto the balance.`} The receipt stays on record; a reversal entry with its own number is added.
        </p>
        <label htmlFor="reverse-reason" className="mt-3 block text-sm font-medium text-text">
          Reason
        </label>
        <Input id="reverse-reason" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Recorded against the wrong student" className="mt-1.5" />
      </ConfirmDialog>
    </>
  );
}

/** Admin: add an adjustment (scholarship/discount or extra charge) and cancel the invoice. */
export function InvoiceAdminActions({ invoice }: { invoice: InvoiceDetail }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [kind, setKind] = useState<"discount" | "charge">("discount");
  const [description, setDescription] = useState("");
  const [amount, setAmount] = useState("");
  const [busy, setBusy] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [reason, setReason] = useState("");
  const hasMoney = invoice.paid !== 0;
  const parsed = parseAmount(amount);

  return (
    <div className="flex flex-wrap gap-2">
      <FormDialog
        title="Add adjustment"
        description={`${invoice.number} · total ${formatNaira(invoice.total)}`}
        trigger={
          <Button variant="secondary">
            <Plus className="size-4" aria-hidden />
            Add adjustment
          </Button>
        }
      >
        {(close) => (
          <div className="space-y-4">
            <div role="radiogroup" aria-label="Kind" className="grid grid-cols-2 gap-2">
              {(
                [
                  ["discount", "Scholarship / discount", "Reduces the total"],
                  ["charge", "Extra charge", "Adds to the total"],
                ] as const
              ).map(([k, label, hint]) => (
                <label key={k} htmlFor={`adj-${k}`} className={cn("flex cursor-pointer gap-2 rounded-input border p-3 text-sm", kind === k ? "border-brand bg-brand/5" : "border-border")}>
                  <input id={`adj-${k}`} type="radio" name="adj-kind" aria-label={label} className="mt-0.5 size-4 accent-[var(--brand)]" checked={kind === k} onChange={() => setKind(k)} />
                  <span>
                    <span className="block font-medium">{label}</span>
                    <span className="block text-xs text-muted">{hint}</span>
                  </span>
                </label>
              ))}
            </div>
            <Field id="adj-desc" label="Description">
              <Input id="adj-desc" value={description} onChange={(e) => setDescription(e.target.value)} placeholder={kind === "discount" ? "e.g. Academic scholarship (50%)" : "e.g. Replacement ID card"} />
            </Field>
            <Field id="adj-amount" label="Amount (₦)" error={amount && (parsed === null || parsed <= 0) ? "Enter an amount above ₦0 with at most 2 decimal places." : undefined}>
              <Input id="adj-amount" value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="tabular" />
            </Field>
            {parsed !== null && parsed > 0 && (
              <p className="text-sm text-muted">
                New total <span className="tabular font-medium text-text">{formatNaira(invoice.total + (kind === "discount" ? -parsed : parsed))}</span>
              </p>
            )}
            <DialogActions>
              <CancelButton />
              <Button
                loading={busy}
                loadingText="Saving…"
                disabled={!description.trim() || parsed === null || parsed <= 0}
                onClick={async () => {
                  setBusy(true);
                  const r = await runAction(() => addAdjustment(invoice.id, description.trim(), kind === "discount" ? -parsed! : parsed!), {
                    success: "Adjustment added",
                    onProblem: showProblem("Couldn't add the adjustment"),
                  });
                  setBusy(false);
                  if (r) {
                    setDescription("");
                    setAmount("");
                    close();
                    startTransition(() => router.refresh());
                  }
                }}
              >
                Add adjustment
              </Button>
            </DialogActions>
          </div>
        )}
      </FormDialog>
      <span title={hasMoney ? "Reverse the payments first" : undefined}>
        <Button variant="ghost" className="text-danger" disabled={hasMoney} onClick={() => setCancelling(true)}>
          <Ban className="size-4" aria-hidden />
          Cancel invoice
        </Button>
      </span>
      <ConfirmDialog
        open={cancelling}
        onOpenChange={setCancelling}
        title={`Cancel ${invoice.number}?`}
        confirmLabel="Cancel invoice"
        pendingLabel="Cancelling…"
        tone="destructive"
        onConfirm={async () => {
          if (!reason.trim()) {
            toast.error("Give a reason for cancelling.");
            return false;
          }
          const r = await runAction(() => cancelInvoice(invoice.id, reason.trim()), { success: `${invoice.number} cancelled`, onProblem: showProblem("Couldn't cancel the invoice") });
          if (r) startTransition(() => router.refresh());
          return r !== null;
        }}
      >
        <p>The invoice stays on record as cancelled. {invoice.studentName} can be invoiced again with Generate invoices.</p>
        <label htmlFor="cancel-reason" className="mt-3 block text-sm font-medium text-text">
          Reason
        </label>
        <Input id="cancel-reason" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Student withdrew before term started" className="mt-1.5" />
      </ConfirmDialog>
      {hasMoney && <Badge>Cancel needs the payments reversed first</Badge>}
    </div>
  );
}
