"use client";

import { useState } from "react";
import { CreditCard, Lock } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { InvoiceDetail } from "@/lib/api/types";
import { onlineAmountError } from "@/lib/finance/payments";
import { formatNaira, parseAmount } from "@/lib/finance/rules";
import { startOnlinePayment } from "@/lib/portal/actions";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Field, Input, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, DialogActions, FormDialog } from "@/components/ui/dialog";

/**
 * E10.5: pay an invoice online. Choose the full balance or a part payment of at least the next
 * instalment, then go to Paystack's hosted checkout (no card details touch this app).
 */
export function PayOnlineButton({ studentId, invoice }: { studentId: string; invoice: Pick<InvoiceDetail, "id" | "number" | "balance" | "minimumPayment" | "instalmentsAllowed"> }) {
  const canSplit = invoice.instalmentsAllowed && invoice.minimumPayment > 0 && invoice.minimumPayment < invoice.balance;
  const [choice, setChoice] = useState<"full" | "part">("full");
  const [amount, setAmount] = useState(invoice.minimumPayment.toFixed(2));
  const [problem, setProblem] = useState<Problem | null>(null);
  const [unavailable, setUnavailable] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const error = choice === "part" ? onlineAmountError(amount, invoice) : null;

  if (unavailable) {
    return (
      <p className="flex items-start gap-1.5 text-sm text-muted">
        <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
        {unavailable}
      </p>
    );
  }

  const pay = async () => {
    setProblem(null);
    if (error) return;
    setBusy(true);
    let result: Awaited<ReturnType<typeof startOnlinePayment>>;
    try {
      result = await startOnlinePayment(studentId, invoice.id, choice === "full" ? null : parseAmount(amount));
    } catch {
      setBusy(false);
      setProblem(unreachableProblem());
      return;
    }
    if (result.ok) {
      // Leave for Paystack's hosted checkout; it sends the parent back to /portal/fees/paid.
      window.location.assign(result.data.authorizationUrl);
      return;
    }
    setBusy(false);
    if (result.problem.code === "payment.online_unavailable") setUnavailable("Online payment isn't available yet. Pay at the bursary or by bank transfer for now.");
    else setProblem(result.problem);
  };

  return (
    <FormDialog
      title="Pay online"
      description={`${invoice.number} · balance ${formatNaira(invoice.balance)}`}
      trigger={
        <Button size="sm">
          <CreditCard className="size-4" aria-hidden />
          Pay online
        </Button>
      }
    >
      {() => (
        <div className="space-y-4">
          {problem && problem.status !== 429 && (
            <Alert tone="danger" title="Couldn't start the payment">
              {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
            </Alert>
          )}
          <div role="radiogroup" aria-label="Amount" className="space-y-2">
            {(
              [
                ["full", `Full balance ${formatNaira(invoice.balance)}`, "Clears this invoice."],
                ...(canSplit ? ([["part", "Next instalment", `At least ${formatNaira(invoice.minimumPayment)}; you can pay more.`]] as const) : []),
              ] as const
            ).map(([key, label, hint]) => (
              <label key={key} htmlFor={`pay-${key}`} className={cn("flex cursor-pointer gap-2.5 rounded-input border p-3 text-sm", choice === key ? "border-brand bg-brand/5" : "border-border")}>
                <input id={`pay-${key}`} type="radio" name="pay-choice" aria-label={label} className="mt-0.5 size-4 accent-[var(--brand)]" checked={choice === key} onChange={() => setChoice(key)} />
                <span>
                  <span className="tabular block font-medium">{label}</span>
                  <span className="block text-xs text-muted">{hint}</span>
                </span>
              </label>
            ))}
          </div>
          {choice === "part" && (
            <Field id="pay-amount" label="Amount (₦)" error={error ?? undefined}>
              <Input {...describedBy("pay-amount", error ?? undefined)} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="tabular" />
            </Field>
          )}
          <p className="flex items-start gap-1.5 text-xs text-muted">
            <Lock className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            You&apos;ll pay on Paystack&apos;s secure page, then come back here. Your card details never reach the school.
          </p>
          <DialogActions>
            <CancelButton />
            <Button onClick={pay} loading={busy} loadingText="Opening Paystack…" disabled={Boolean(error)}>
              <CreditCard className="size-4" aria-hidden />
              Continue to Paystack
            </Button>
          </DialogActions>
        </div>
      )}
    </FormDialog>
  );
}
