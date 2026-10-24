"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import { Pencil, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { correctDay } from "@/lib/clock/actions";
import { longDay, type StaffDay } from "@/lib/clock/clock";
import { lagosIso, toLagosParts } from "@/lib/time/lagos";
import { FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";
const time = (iso: string | null) => (iso ? toLagosParts(iso).time : "");

/** Admin: set or clear a day's in/out times (WAT). A note is required; the change is audited. */
export function CorrectDayButton({ day }: { day: StaffDay }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [clockIn, setClockIn] = useState(time(day.clockInAt));
  const [clockOut, setClockOut] = useState(time(day.clockOutAt));
  const [note, setNote] = useState(day.note ?? "");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [open, setOpen] = useState(false);
  // Each time the dialog opens, the form starts from the saved values.
  const onOpenChange = (v: boolean) => {
    if (v) {
      setClockIn(time(day.clockInAt));
      setClockOut(time(day.clockOutAt));
      setNote(day.note ?? "");
      setProblem(null);
    }
    setOpen(v);
  };
  const server = problem?.errors ?? {};
  const local = {
    clockIn: !clockIn && clockOut ? "Add the time in as well." : undefined,
    clockOut: clockIn && clockOut && clockOut <= clockIn ? "Out must be after in." : undefined,
    note: note.trim() === "" ? "Say why the times are being changed." : note.length > 500 ? "500 characters at most." : undefined,
  };
  const errors = {
    clockIn: local.clockIn ?? server.clockInAt?.[0],
    clockOut: local.clockOut ?? server.clockOutAt?.[0],
    note: local.note ?? server.note?.[0],
  };
  const id = `fix-${day.staffId}`;

  return (
    <FormDialog
      title={`Correct ${day.name}'s day`}
      description={`${longDay(day.date)} · Abuja time (WAT)`}
      open={open}
      onOpenChange={onOpenChange}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Correct ${day.name}'s day`}>
          <Pencil className="size-4" aria-hidden />
          <span className="hidden sm:inline">Correct</span>
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4 p-5">
          {problem && !problem.errors && (
            <Alert tone="danger" title="Couldn't save">
              {problemMessage(problem)}
            </Alert>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id={`${id}-in`} label="Time in" error={errors.clockIn} hint="Leave empty to mark absent">
              <Input type="time" value={clockIn} onChange={(e) => setClockIn(e.target.value)} {...describedBy(`${id}-in`, errors.clockIn, true)} />
            </Field>
            <Field id={`${id}-out`} label="Time out" error={errors.clockOut}>
              <Input type="time" value={clockOut} onChange={(e) => setClockOut(e.target.value)} {...describedBy(`${id}-out`, errors.clockOut)} />
            </Field>
          </div>
          <Field id={`${id}-note`} label="Reason" error={errors.note} hint="For example: forgot to scan, phone battery died. Kept in the audit log.">
            <textarea value={note} onChange={(e) => setNote(e.target.value)} rows={3} className={textareaClass} {...describedBy(`${id}-note`, errors.note, true)} />
          </Field>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Boolean(local.clockIn || local.clockOut || local.note)}
              onClick={async () => {
                setProblem(null);
                setBusy(true);
                const r = await runAction(
                  () =>
                    correctDay(day.staffId, day.date, {
                      clockInAt: clockIn ? lagosIso(day.date, clockIn) : null,
                      clockOutAt: clockOut ? lagosIso(day.date, clockOut) : null,
                      note: note.trim(),
                    }),
                  { success: `${day.name}'s day updated`, onProblem: setProblem },
                );
                setBusy(false);
                if (r) {
                  close();
                  startTransition(() => router.refresh());
                }
              }}
            >
              <Save className="size-4" aria-hidden />
              Save
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

/** Refreshes the board every minute while it shows today. */
export function AutoRefresh({ seconds = 60 }: { seconds?: number }) {
  const router = useRouter();
  useEffect(() => {
    const id = window.setInterval(() => {
      if (document.visibilityState === "visible") router.refresh();
    }, seconds * 1000);
    return () => window.clearInterval(id);
  }, [router, seconds]);
  return null;
}
