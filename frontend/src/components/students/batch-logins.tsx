"use client";

import { useState } from "react";
import { KeyRound } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import type { StudentListItem } from "@/lib/api/types";
import { issueStudentLogin } from "@/lib/students/actions";
import { Button } from "@/components/ui/primitives";
import { FormDialog } from "@/components/ui/dialog";
import { Alert } from "@/components/ui/states";
import { CredentialSlips, type SlipPerson } from "@/components/credentials/credential-slip";

/**
 * Issue logins for a class, one POST per student (there's no bulk endpoint), then print every slip.
 * Only active students without a login are offered.
 */
export function BatchLoginsButton({ className, students }: { className: string; students: StudentListItem[] }) {
  const candidates = students.filter((s) => s.status === "Active" && !s.hasLogin);
  const [open, setOpen] = useState(false);
  const [chosen, setChosen] = useState<ReadonlySet<string>>(new Set());
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null);
  const [slips, setSlips] = useState<SlipPerson[] | null>(null);
  const [failures, setFailures] = useState<string[]>([]);

  if (candidates.length === 0) return null;

  const reset = () => {
    setChosen(new Set(candidates.map((s) => s.id)));
    setProgress(null);
    setSlips(null);
    setFailures([]);
  };

  const run = async () => {
    const list = candidates.filter((s) => chosen.has(s.id));
    const out: SlipPerson[] = [];
    const failed: string[] = [];
    setProgress({ done: 0, total: list.length });
    for (const [i, s] of list.entries()) {
      try {
        const r = await issueStudentLogin(s.id);
        if (r.ok) out.push({ fullName: s.fullName, description: `${s.admissionNumber} · ${className}`, ...r.data });
        else failed.push(`${s.fullName}: ${problemMessage(r.problem)}`);
      } catch {
        failed.push(`${s.fullName}: network error`);
      }
      setProgress({ done: i + 1, total: list.length });
    }
    setFailures(failed);
    setSlips(out);
  };

  return (
    <FormDialog
      open={open}
      onOpenChange={(v) => {
        if (progress && !slips) return; // don't close mid-run
        setOpen(v);
        if (v) reset();
        else setSlips(null);
      }}
      title={slips ? "Sign-in slips" : `Issue logins for ${className}`}
      description={slips ? `${slips.length} issued` : `${candidates.length} active students on this page have no login.`}
      trigger={
        <Button variant="secondary">
          <KeyRound className="size-4" aria-hidden />
          Issue class logins
        </Button>
      }
    >
      {(close) =>
        slips ? (
          <div className="space-y-4">
            {failures.length > 0 && (
              <Alert tone="warning" title={`${failures.length} couldn't be issued`}>
                <ul className="list-disc pl-4">
                  {failures.map((f) => (
                    <li key={f}>{f}</li>
                  ))}
                </ul>
              </Alert>
            )}
            {slips.length > 0 ? <CredentialSlips people={slips} onDone={close} /> : <Button onClick={close}>Close</Button>}
          </div>
        ) : (
          <div className="space-y-4">
            <fieldset>
              <legend className="mb-2 flex w-full items-center justify-between text-sm font-medium">
                Students
                <button
                  type="button"
                  className="text-xs font-normal text-brand hover:underline"
                  onClick={() => setChosen(chosen.size === candidates.length ? new Set() : new Set(candidates.map((s) => s.id)))}
                >
                  {chosen.size === candidates.length ? "Select none" : "Select all"}
                </button>
              </legend>
              <ul className="max-h-72 divide-y divide-border overflow-y-auto rounded-input border border-border">
                {candidates.map((s) => (
                  <li key={s.id}>
                    <label className="flex min-h-11 cursor-pointer items-center gap-3 px-3">
                      <input
                        type="checkbox"
                        className="size-4 accent-[var(--brand)]"
                        checked={chosen.has(s.id)}
                        disabled={progress !== null}
                        onChange={(e) => {
                          const next = new Set(chosen);
                          if (e.target.checked) next.add(s.id);
                          else next.delete(s.id);
                          setChosen(next);
                        }}
                      />
                      <span className="flex-1">{s.fullName}</span>
                      <span className="font-mono text-xs text-muted">{s.admissionNumber}</span>
                    </label>
                  </li>
                ))}
              </ul>
            </fieldset>
            {progress && (
              <div aria-live="polite">
                <p className="mb-1 text-sm">
                  Issuing {progress.done} of {progress.total}…
                </p>
                <progress className="h-2 w-full accent-[var(--brand)]" value={progress.done} max={progress.total} />
              </div>
            )}
            <div className="flex justify-end">
              <Button onClick={run} loading={progress !== null} disabled={chosen.size === 0}>
                Issue {chosen.size} login{chosen.size === 1 ? "" : "s"}
              </Button>
            </div>
          </div>
        )
      }
    </FormDialog>
  );
}
