"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import { Camera, FileUp, Landmark, Upload } from "lucide-react";
import { isProblem, problemMessage, toProblem, type Problem } from "@/lib/api/problem";
import { PROOF_ACCEPT, proofErrors, proofFileError, type ProofErrors } from "@/lib/finance/proofs";
import { formatNaira, parseAmount } from "@/lib/finance/rules";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, DialogActions, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

/**
 * E10.8: "I've paid by bank transfer". A photo (camera or file) or PDF of the transfer, the amount
 * (prefilled with the balance), the date and bank reference. The bursar checks it and the receipt is
 * emailed. Also used for "Send again" after a proof wasn't accepted.
 */
export function ProofUploadButton({ studentId, invoiceId, invoiceNumber, balance, today, label = "I've paid by bank transfer" }: { studentId: string; invoiceId: string; invoiceNumber: string; balance: number; today: string; label?: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const fileInput = useRef<HTMLInputElement>(null);
  const cameraInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [amount, setAmount] = useState(balance.toFixed(2));
  const [paidOn, setPaidOn] = useState(today);
  const [bankReference, setBankReference] = useState("");
  const [note, setNote] = useState("");
  const [touched, setTouched] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => () => {
    if (preview) URL.revokeObjectURL(preview);
  }, [preview]);

  const fieldErrors: ProofErrors = touched ? proofErrors({ amount, paidOn, bankReference, note }, balance, today) : {};
  const fileErr = touched ? proofFileError(file) : file ? proofFileError(file) : null;

  const choose = (f: File | undefined) => {
    if (!f) return;
    setFile(f);
    setPreview(f.type.startsWith("image/") ? URL.createObjectURL(f) : null);
  };

  const reset = () => {
    setFile(null);
    setPreview(null);
    setAmount(balance.toFixed(2));
    setPaidOn(today);
    setBankReference("");
    setNote("");
    setTouched(false);
    setProblem(null);
  };

  const send = async (close: () => void) => {
    setTouched(true);
    setProblem(null);
    if (proofFileError(file) || Object.keys(proofErrors({ amount, paidOn, bankReference, note }, balance, today)).length > 0) return;
    const form = new FormData();
    form.append("file", file!);
    form.append("amount", String(parseAmount(amount)));
    form.append("paidOn", paidOn);
    if (bankReference.trim()) form.append("bankReference", bankReference.trim());
    if (note.trim()) form.append("note", note.trim());
    setBusy(true);
    try {
      const res = await fetch(`/api/media/portal/children/${studentId}/invoices/${invoiceId}/proofs`, { method: "POST", body: form, credentials: "same-origin" });
      if (!res.ok) {
        const json: unknown = await res.json().catch(() => null);
        setProblem(isProblem(json) ? json : toProblem(res.status, json));
        return;
      }
      toast.success("Proof sent", { description: "The school will check your payment and send your receipt." });
      close();
      reset();
      startTransition(() => router.refresh());
    } catch {
      setProblem(unreachableProblem());
    } finally {
      setBusy(false);
    }
  };

  return (
    <FormDialog
      title="I've paid by bank transfer"
      description={`${invoiceNumber} · balance ${formatNaira(balance)}`}
      onOpenChange={(v) => !v && !busy && reset()}
      trigger={
        <Button size="sm" variant="secondary">
          <Landmark className="size-4" aria-hidden />
          {label}
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4">
          <p className="text-sm text-muted">Send a photo or PDF of your transfer. The school will check your payment and send your receipt.</p>
          {problem && problem.status !== 429 && (
            <Alert tone="danger" title="Couldn't send the proof">
              {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
            </Alert>
          )}
          <div>
            <input ref={fileInput} type="file" accept={PROOF_ACCEPT} className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => choose(e.target.files?.[0])} />
            <input ref={cameraInput} type="file" accept="image/*" capture="environment" className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => choose(e.target.files?.[0])} />
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" onClick={() => fileInput.current?.click()}>
                <FileUp className="size-4" aria-hidden />
                {file ? "Choose another file" : "Choose photo or PDF"}
              </Button>
              <Button variant="secondary" onClick={() => cameraInput.current?.click()}>
                <Camera className="size-4" aria-hidden />
                Take a photo
              </Button>
            </div>
            {file && (
              <div className="mt-3 flex items-center gap-3 rounded-input border border-border p-2 text-sm">
                {/* Local object-URL preview of the parent's own photo; PDFs are never rendered. */}
                {preview ? <img src={preview} alt="Your proof (not sent yet)" className="size-16 rounded object-cover" /> : <FileUp className="size-8 text-muted" aria-hidden />}
                <span className="min-w-0 flex-1 truncate">{file.name}</span>
              </div>
            )}
            {fileErr && (
              <p className="mt-1.5 text-sm text-danger" role="alert">
                {fileErr}
              </p>
            )}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="proof-amount" label="Amount paid (₦)" error={fieldErrors.amount}>
              <Input {...describedBy("proof-amount", fieldErrors.amount)} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="tabular" />
            </Field>
            <Field id="proof-date" label="Date paid" error={fieldErrors.paidOn}>
              <Input {...describedBy("proof-date", fieldErrors.paidOn)} type="date" value={paidOn} max={today} onChange={(e) => setPaidOn(e.target.value)} />
            </Field>
            <Field id="proof-ref" label="Bank reference (optional)" error={fieldErrors.bankReference} className="sm:col-span-2">
              <Input {...describedBy("proof-ref", fieldErrors.bankReference)} value={bankReference} onChange={(e) => setBankReference(e.target.value)} autoComplete="off" placeholder="From your bank's receipt" />
            </Field>
            <Field id="proof-note" label="Note (optional)" error={fieldErrors.note} className="sm:col-span-2">
              <Input {...describedBy("proof-note", fieldErrors.note)} value={note} onChange={(e) => setNote(e.target.value)} autoComplete="off" placeholder="e.g. Paid from my husband's account" />
            </Field>
          </div>
          <DialogActions>
            <CancelButton />
            <Button onClick={() => send(close)} loading={busy} loadingText="Sending…">
              <Upload className="size-4" aria-hidden />
              Send proof
            </Button>
          </DialogActions>
        </div>
      )}
    </FormDialog>
  );
}
