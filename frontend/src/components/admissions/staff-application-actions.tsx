"use client";

import Link from "next/link";
import { useState } from "react";
import { CheckCircle2, RotateCcw, UserPlus } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { enrolApplicant, reopenApplication } from "@/lib/admissions/actions";
import { runAction } from "@/lib/client/action";
import { Button, Field, Select, buttonClasses } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";

/** Admin: give a family who missed the exam a new window (E11.6). They're emailed. */
export function ReopenButton({ applicationId, childName }: { applicationId: string; childName: string }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button size="sm" variant="secondary" onClick={() => setOpen(true)}>
        <RotateCcw className="size-4" aria-hidden />
        Reopen
      </Button>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title={`Reopen ${childName}'s application?`}
        confirmLabel="Reopen"
        pendingLabel="Reopening…"
        onConfirm={async () => (await runAction(() => reopenApplication(applicationId), { success: "Application reopened; the family has been emailed" })) !== null}
      >
        <p>The family gets a new window to book the entrance exam.</p>
      </ConfirmDialog>
    </>
  );
}

/** Admin: enrol an accepted applicant (E11.8): creates the student and guardians; the login becomes a parent login. */
export function EnrolButton({ applicationId, childName, classrooms }: { applicationId: string; childName: string; classrooms: { id: string; name: string; full: boolean }[] }) {
  const [classRoomId, setClassRoomId] = useState(classrooms.find((c) => !c.full)?.id ?? "");
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<{ studentId: string; admissionNumber: string } | null>(null);

  return (
    <FormDialog
      title={done ? `${childName} is enrolled` : `Enrol ${childName}`}
      description={done ? undefined : "Creates the student record and links the guardians. New-intake fees are billed when you next generate invoices."}
      onOpenChange={(v) => !v && setProblem(null)}
      trigger={
        <Button size="sm">
          <UserPlus className="size-4" aria-hidden />
          Enrol
        </Button>
      }
    >
      {() =>
        done ? (
          <div className="space-y-4">
            <Alert tone="success" title={`Admission number ${done.admissionNumber}`}>
              <span className="flex items-center gap-1.5">
                <CheckCircle2 className="size-4" aria-hidden />
                The family can now sign in to the parent portal with the same account.
              </span>
            </Alert>
            <DialogActions>
              <Link href={`/admin/students/${done.studentId}`} className={buttonClasses("primary")}>
                Open the student record
              </Link>
            </DialogActions>
          </div>
        ) : (
          <div className="space-y-4">
            {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
              <Alert tone="danger" title="Couldn't enrol">
                {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
              </Alert>
            )}
            <Field id="enrol-class" label="Class" hint={classrooms.length === 0 ? "No classes at this level yet; the student is enrolled without a class." : undefined}>
              <Select id="enrol-class" value={classRoomId} onChange={(e) => setClassRoomId(e.target.value)} disabled={classrooms.length === 0}>
                <option value="">No class for now</option>
                {classrooms.map((c) => (
                  <option key={c.id} value={c.id} disabled={c.full}>
                    {c.name}
                    {c.full ? " (full)" : ""}
                  </option>
                ))}
              </Select>
            </Field>
            <DialogActions>
              <CancelButton />
              <Button
                loading={busy}
                loadingText="Enrolling…"
                onClick={async () => {
                  setProblem(null);
                  setBusy(true);
                  const r = await runAction(() => enrolApplicant(applicationId, classRoomId || null), { success: (x) => `${childName} enrolled as ${x.admissionNumber}`, onProblem: setProblem });
                  setBusy(false);
                  if (r) setDone({ studentId: r.studentId, admissionNumber: r.admissionNumber });
                }}
              >
                <UserPlus className="size-4" aria-hidden />
                Enrol
              </Button>
            </DialogActions>
          </div>
        )
      }
    </FormDialog>
  );
}
