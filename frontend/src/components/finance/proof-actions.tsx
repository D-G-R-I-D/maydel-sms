"use client";

import { useEffect, useState } from "react";
import { CheckCircle2, Download, Eye, X } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { Proof } from "@/lib/api/types";
import { confirmProof, rejectProof } from "@/lib/finance/actions";
import { formatNaira, parseAmount } from "@/lib/finance/rules";
import { runAction } from "@/lib/client/action";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";
import { LoaderStatus } from "@/components/ui/school-loader";
import { PdfDownloadButton } from "@/components/reports/pdf-download";

const fileHref = (id: string) => `/api/media/finance/proofs/${id}/file`;

/**
 * View a parent's uploaded proof. Images preview from a blob URL; PDFs (untrusted) are only ever
 * downloaded, never shown inline. The BFF serves both as attachments.
 */
export function ProofViewButton({ proof }: { proof: Proof }) {
  const isImage = proof.contentType.startsWith("image/");
  const [open, setOpen] = useState(false);
  const [url, setUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !isImage) return;
    let live = true;
    let made: string | null = null;
    fetch(fileHref(proof.id), { credentials: "same-origin" })
      .then(async (res) => {
        if (!res.ok) throw new Error((await res.text().catch(() => "")) || "The file couldn't be loaded.");
        const blob = await res.blob();
        made = URL.createObjectURL(blob);
        if (live) setUrl(made);
      })
      .catch((e: unknown) => live && setError(e instanceof Error && e.message ? e.message : unreachableProblem().title));
    return () => {
      live = false;
      if (made) URL.revokeObjectURL(made);
    };
  }, [open, isImage, proof.id]);

  const download = (
    <a href={fileHref(proof.id)} download className="inline-flex min-h-9 items-center gap-1.5 rounded-input px-3 text-sm text-brand hover:bg-surface-2">
      <Download className="size-4" aria-hidden />
      Download
    </a>
  );
  if (!isImage) return download;

  return (
    <FormDialog
      open={open}
      onOpenChange={(v) => {
        setOpen(v);
        if (!v) {
          setUrl(null);
          setError(null);
        }
      }}
      size="lg"
      title={`Proof from ${proof.studentName}`}
      description={`${proof.invoiceNumber} · ${formatNaira(proof.amount)} paid ${proof.paidOn}${proof.bankReference ? ` · ref ${proof.bankReference}` : ""}`}
      trigger={
        <Button variant="ghost" size="sm">
          <Eye className="size-4" aria-hidden />
          View proof
        </Button>
      }
    >
      {() => (
        <div className="space-y-3">
          {error ? (
            <Alert tone="danger" title="Couldn't load the proof">
              {error}
            </Alert>
          ) : url ? (
            // A local blob URL of the uploaded photo (never a PDF).
            <img src={url} alt={`Proof of payment from ${proof.studentName}`} className="mx-auto max-h-[60dvh] rounded-input border border-border object-contain" />
          ) : (
            <LoaderStatus label="Loading proof…" />
          )}
          {proof.note && <p className="text-sm">Parent&apos;s note: {proof.note}</p>}
          <div className="flex justify-end">{download}</div>
        </div>
      )}
    </FormDialog>
  );
}

const showProblem = (title: string) => (p: Problem) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error(title, { description: p.errors ? Object.values(p.errors).flat().join(" ") : problemMessage(p) });

/** Confirm (prefilled from the proof, editable) → receipt panel; Reject with a required reason. */
export function ProofReviewButtons({ proof, today }: { proof: Proof; today: string }) {
  const [amount, setAmount] = useState(proof.amount.toFixed(2));
  const [method, setMethod] = useState<"BankTransfer" | "Cash" | "Pos">("BankTransfer");
  const [paidOn, setPaidOn] = useState(proof.paidOn);
  const [busy, setBusy] = useState(false);
  const [confirmed, setConfirmed] = useState<Proof | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState("");

  const n = parseAmount(amount);
  const amountError = n === null || n <= 0 ? "Enter an amount above ₦0." : n > proof.invoiceBalance ? `More than the invoice balance of ${formatNaira(proof.invoiceBalance)}.` : null;
  const dateError = paidOn > today ? "The payment date can't be in the future." : null;

  return (
    <div className="flex flex-wrap gap-1.5">
      <FormDialog
        title={confirmed ? "Payment recorded" : "Confirm payment"}
        description={`${proof.studentName} · ${proof.admissionNumber} · ${proof.invoiceNumber}`}
        trigger={
          <Button size="sm">
            <CheckCircle2 className="size-4" aria-hidden />
            Confirm
          </Button>
        }
      >
        {(close) =>
          confirmed ? (
            <div className="space-y-4">
              <div className="rounded-card border border-success/30 bg-success/8 p-4 text-sm">
                <p className="flex items-center gap-2 font-medium text-success">
                  <CheckCircle2 className="size-5" aria-hidden />
                  Receipt {confirmed.receiptNumber}
                </p>
                <p className="mt-2">
                  {formatNaira(confirmed.amount)} recorded for {confirmed.studentName} (<span className="tabular font-semibold">{confirmed.admissionNumber}</span>). The receipt has been emailed to the parents.
                </p>
              </div>
              <DialogActions>
                {confirmed.paymentId && (
                  <PdfDownloadButton href={`/api/media/finance/receipts/${confirmed.paymentId}/pdf`} label="Download receipt" fallbackName={`${confirmed.receiptNumber ?? "Receipt"}.pdf`} showView={false} />
                )}
                <Button onClick={close}>Done</Button>
              </DialogActions>
            </div>
          ) : (
            <div className="space-y-4">
              {problem && problem.status > 0 && problem.status !== 429 && (
                <Alert tone="danger" title="Couldn't confirm">
                  {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
                </Alert>
              )}
              <p className="text-sm text-muted">Check the amount reached the school&apos;s account before confirming. A receipt is recorded and emailed to the parents.</p>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field id={`cf-amount-${proof.id}`} label="Amount received (₦)" error={amountError ?? undefined} hint={`Invoice balance ${formatNaira(proof.invoiceBalance)}`}>
                  <Input {...describedBy(`cf-amount-${proof.id}`, amountError ?? undefined, true)} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="tabular" />
                </Field>
                <Field id={`cf-method-${proof.id}`} label="Method">
                  <Select id={`cf-method-${proof.id}`} value={method} onChange={(e) => setMethod(e.target.value as typeof method)}>
                    <option value="BankTransfer">Bank transfer</option>
                    <option value="Cash">Cash</option>
                    <option value="Pos">POS</option>
                  </Select>
                </Field>
                <Field id={`cf-date-${proof.id}`} label="Paid on" error={dateError ?? undefined}>
                  <Input {...describedBy(`cf-date-${proof.id}`, dateError ?? undefined)} type="date" value={paidOn} max={today} onChange={(e) => setPaidOn(e.target.value)} />
                </Field>
              </div>
              <DialogActions>
                <CancelButton />
                <Button
                  loading={busy}
                  loadingText="Confirming…"
                  disabled={Boolean(amountError || dateError)}
                  onClick={async () => {
                    setProblem(null);
                    setBusy(true);
                    const r = await runAction(() => confirmProof(proof.id, { amount: n, method, paidOn }), {
                      success: (x) => `Payment confirmed · ${x.receiptNumber ?? ""}`.trim(),
                      onProblem: setProblem,
                    });
                    setBusy(false);
                    if (r) setConfirmed(r);
                  }}
                >
                  <CheckCircle2 className="size-4" aria-hidden />
                  Confirm and record
                </Button>
              </DialogActions>
            </div>
          )
        }
      </FormDialog>
      <Button size="sm" variant="ghost" className="text-danger" onClick={() => setRejecting(true)}>
        <X className="size-4" aria-hidden />
        Reject
      </Button>
      <ConfirmDialog
        open={rejecting}
        onOpenChange={setRejecting}
        title="Reject this proof?"
        confirmLabel="Reject proof"
        pendingLabel="Rejecting…"
        tone="destructive"
        onConfirm={async () => {
          if (!reason.trim()) {
            toast.error("Give a reason; it's emailed to the parent.");
            return false;
          }
          const r = await runAction(() => rejectProof(proof.id, reason.trim()), { success: "Proof rejected; the parent has been told why", onProblem: showProblem("Couldn't reject the proof") });
          return r !== null;
        }}
      >
        <p>The parent is emailed the reason and can send a new proof.</p>
        <label htmlFor={`rj-reason-${proof.id}`} className="mt-3 block text-sm font-medium text-text">
          Reason
        </label>
        <Input id={`rj-reason-${proof.id}`} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. No matching credit in the school account" className="mt-1.5" />
      </ConfirmDialog>
    </div>
  );
}
