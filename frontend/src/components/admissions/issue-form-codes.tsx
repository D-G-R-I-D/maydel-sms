"use client";

import { useState } from "react";
import { Copy, KeyRound, Printer } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { FormCode } from "@/lib/api/types";
import { issueFormCodes } from "@/lib/admissions/actions";
import { formatNaira, parseAmount } from "@/lib/finance/rules";
import { runAction } from "@/lib/client/action";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, DialogActions, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

const escape = (s: string) => s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]!);

/** A print-friendly page of slips (one per code) in a new window; nothing leaves the browser. */
function printSlips(codes: FormCode[], session: string) {
  const w = window.open("", "_blank", "width=720,height=900");
  if (!w) {
    toast.error("Allow pop-ups to print the slips.");
    return;
  }
  const slips = codes
    .map(
      (c) => `<section class="slip">
  <p class="school">Maydel Spring Schools: admission form code</p>
  <p class="code">${escape(c.code)}</p>
  <p>Session ${escape(session)} · Paid ${escape(formatNaira(c.amountPaid))}${c.issuedTo ? ` · Issued to ${escape(c.issuedTo)}` : ""}</p>
  <p class="how">Create your family account at the school's admissions page, then enter this code under “I have a form code”. It can be used once.</p>
</section>`,
    )
    .join("");
  w.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>Form codes</title><style>
body{font-family:system-ui,sans-serif;margin:24px;color:#111}
.slip{border:1px dashed #555;border-radius:8px;padding:16px 20px;margin:0 0 16px;page-break-inside:avoid}
.school{font-weight:600;margin:0 0 8px}.code{font:700 36px/1.2 ui-monospace,monospace;letter-spacing:.12em;margin:8px 0}
.how{font-size:13px;color:#444}p{margin:4px 0}
</style></head><body>${slips}<script>window.onload=()=>window.print()</script></body></html>`);
  w.document.close();
}

/** E11.4: issue form codes after a family pays at the school. Shown once, large and copyable, with printable slips. */
export function IssueFormCodes({ sessionId, session }: { sessionId: string; session: string }) {
  const [count, setCount] = useState("1");
  const [amount, setAmount] = useState("");
  const [issuedTo, setIssuedTo] = useState("");
  const [issued, setIssued] = useState<FormCode[] | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const n = Number(count);
  const amt = parseAmount(amount);
  const countError = !Number.isInteger(n) || n < 1 || n > 50 ? "Issue between 1 and 50 codes at a time." : undefined;
  const amountError = amount && amt === null ? "Enter the amount paid in ₦." : undefined;

  return (
    <FormDialog
      title={issued ? `${issued.length} form ${issued.length === 1 ? "code" : "codes"} issued` : "Issue form codes"}
      description={issued ? "Shown once. Give each family its code or print the slips." : `${session} · after the family has paid at the school`}
      onOpenChange={(v) => {
        if (!v) {
          setIssued(null);
          setProblem(null);
        }
      }}
      trigger={
        <Button>
          <KeyRound className="size-4" aria-hidden />
          Issue form codes
        </Button>
      }
    >
      {(close) =>
        issued ? (
          <div className="space-y-4">
            <ul className="space-y-2">
              {issued.map((c) => (
                <li key={c.id} className="flex items-center gap-3 rounded-input border border-border p-3">
                  <span className="flex-1 font-mono text-2xl font-bold tracking-[0.15em] sm:text-3xl">{c.code}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={`Copy ${c.code}`}
                    onClick={async () => {
                      try {
                        await navigator.clipboard.writeText(c.code);
                        toast.success(`${c.code} copied`);
                      } catch {
                        toast.error("Couldn't copy. Select the code and copy it.");
                      }
                    }}
                  >
                    <Copy className="size-4" aria-hidden />
                  </Button>
                </li>
              ))}
            </ul>
            <DialogActions>
              <Button variant="secondary" onClick={() => printSlips(issued, session)}>
                <Printer className="size-4" aria-hidden />
                Print slips
              </Button>
              <Button onClick={close}>Done</Button>
            </DialogActions>
          </div>
        ) : (
          <div className="space-y-4">
            {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
              <Alert tone="danger" title="Couldn't issue codes">
                {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
              </Alert>
            )}
            <div className="grid gap-4 sm:grid-cols-2">
              <Field id="fc-count" label="How many" error={countError}>
                <Input {...describedBy("fc-count", countError)} value={count} onChange={(e) => setCount(e.target.value)} inputMode="numeric" className="tabular" />
              </Field>
              <Field id="fc-amount" label="Amount paid each (₦)" error={amountError}>
                <Input {...describedBy("fc-amount", amountError)} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="tabular" />
              </Field>
              <Field id="fc-to" label="Issued to (optional)" className="sm:col-span-2" hint="e.g. the parent's name or receipt number">
                <Input {...describedBy("fc-to", undefined, true)} value={issuedTo} onChange={(e) => setIssuedTo(e.target.value)} autoComplete="off" />
              </Field>
            </div>
            <DialogActions>
              <CancelButton />
              <Button
                loading={busy}
                loadingText="Issuing…"
                disabled={Boolean(countError || amountError) || amt === null}
                onClick={async () => {
                  setProblem(null);
                  setBusy(true);
                  const r = await runAction(() => issueFormCodes({ sessionId, count: n, amountPaid: amt!, issuedTo: issuedTo.trim() || null }), {
                    success: (x) => `${x.length} form ${x.length === 1 ? "code" : "codes"} issued`,
                    onProblem: setProblem,
                  });
                  setBusy(false);
                  if (r) setIssued(r);
                }}
              >
                Issue
              </Button>
            </DialogActions>
          </div>
        )
      }
    </FormDialog>
  );
}
