"use client";

import { useState } from "react";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { lagosIso, slotErrors, toLagosParts, type ExamMode, type ExamSlot, type SlotInput } from "@/lib/admissions/exam";
import { deleteExamSlot, saveExamSlot } from "@/lib/admissions/actions";
import { runAction } from "@/lib/client/action";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

function SlotForm({ sessionId, slot, close }: { sessionId: string; slot?: ExamSlot; close: () => void }) {
  const start = slot ? toLagosParts(slot.startsAt) : { date: "", time: "09:00" };
  const [v, setV] = useState<SlotInput>({
    date: start.date,
    time: start.time,
    durationMinutes: String(slot?.durationMinutes ?? 120),
    venue: slot?.venue ?? "",
    mode: slot?.mode ?? "Paper",
    capacity: String(slot?.capacity ?? 40),
    notes: slot?.notes ?? "",
  });
  const [touched, setTouched] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const errors = touched ? slotErrors(v) : {};
  const set = (patch: Partial<SlotInput>) => setV((x) => ({ ...x, ...patch }));

  const save = async () => {
    setTouched(true);
    setProblem(null);
    if (Object.keys(slotErrors(v)).length > 0) return;
    setBusy(true);
    const r = await runAction(
      () =>
        saveExamSlot(slot?.id ?? null, {
          sessionId,
          startsAt: lagosIso(v.date, v.time),
          durationMinutes: Number(v.durationMinutes),
          venue: v.venue.trim(),
          mode: v.mode,
          capacity: Number(v.capacity),
          notes: v.notes.trim() || null,
        }),
      { success: slot ? "Sitting saved" : "Sitting added", onProblem: setProblem },
    );
    setBusy(false);
    if (r) close();
  };

  return (
    <div className="space-y-4">
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title="Couldn't save the sitting">
          {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="slot-date" label="Date" error={errors.date}>
          <Input {...describedBy("slot-date", errors.date)} type="date" value={v.date} onChange={(e) => set({ date: e.target.value })} />
        </Field>
        <Field id="slot-time" label="Starts at (Lagos time)" error={errors.time}>
          <Input {...describedBy("slot-time", errors.time)} type="time" value={v.time} onChange={(e) => set({ time: e.target.value })} />
        </Field>
        <Field id="slot-duration" label="Duration (minutes)" error={errors.durationMinutes}>
          <Input {...describedBy("slot-duration", errors.durationMinutes)} inputMode="numeric" value={v.durationMinutes} onChange={(e) => set({ durationMinutes: e.target.value })} className="tabular" />
        </Field>
        <Field id="slot-capacity" label="Seats" error={errors.capacity}>
          <Input {...describedBy("slot-capacity", errors.capacity)} inputMode="numeric" value={v.capacity} onChange={(e) => set({ capacity: e.target.value })} className="tabular" />
        </Field>
        <Field id="slot-venue" label="Venue" error={errors.venue}>
          <Input {...describedBy("slot-venue", errors.venue)} value={v.venue} onChange={(e) => set({ venue: e.target.value })} placeholder="e.g. Main hall" />
        </Field>
        <Field id="slot-mode" label="Mode">
          <Select id="slot-mode" value={v.mode} onChange={(e) => set({ mode: e.target.value as ExamMode })}>
            <option value="Paper">Paper</option>
            <option value="Cbt">Computer-based (CBT)</option>
          </Select>
        </Field>
        <Field id="slot-notes" label="Notes for families (optional)" error={errors.notes} className="sm:col-span-2">
          <Input {...describedBy("slot-notes", errors.notes)} value={v.notes} onChange={(e) => set({ notes: e.target.value })} />
        </Field>
      </div>
      <DialogActions>
        <CancelButton />
        <Button onClick={save} loading={busy} loadingText="Saving…">
          {slot ? "Save sitting" : "Add sitting"}
        </Button>
      </DialogActions>
    </div>
  );
}

/** E11.6 (admin): add an entrance exam sitting. */
export function AddExamSlotButton({ sessionId }: { sessionId: string }) {
  return (
    <FormDialog
      title="Add exam sitting"
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          Add sitting
        </Button>
      }
    >
      {(close) => <SlotForm sessionId={sessionId} close={close} />}
    </FormDialog>
  );
}

/** E11.6 (admin): edit or delete a sitting; both are refused once anyone has booked it. */
export function ExamSlotActions({ sessionId, slot }: { sessionId: string; slot: ExamSlot }) {
  const [removing, setRemoving] = useState(false);
  const booked = slot.booked > 0;
  return (
    <span className="inline-flex gap-1" title={booked ? "Families have booked this sitting, so it can't be moved or deleted" : undefined}>
      <FormDialog
        title="Edit exam sitting"
        trigger={
          <Button variant="ghost" size="sm" disabled={booked} aria-label="Edit sitting">
            <Pencil className="size-4" aria-hidden />
          </Button>
        }
      >
        {(close) => <SlotForm sessionId={sessionId} slot={slot} close={close} />}
      </FormDialog>
      <Button variant="ghost" size="sm" className="text-danger" disabled={booked} onClick={() => setRemoving(true)} aria-label="Delete sitting">
        <Trash2 className="size-4" aria-hidden />
      </Button>
      <ConfirmDialog
        open={removing}
        onOpenChange={setRemoving}
        title="Delete this sitting?"
        confirmLabel="Delete sitting"
        pendingLabel="Deleting…"
        tone="destructive"
        onConfirm={async () => {
          const r = await runAction(() => deleteExamSlot(slot.id), {
            success: "Sitting deleted",
            onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Couldn't delete the sitting", { description: problemMessage(p) }),
          });
          return r !== null;
        }}
      >
        <p>Nobody has booked it yet, so no family is affected.</p>
      </ConfirmDialog>
    </span>
  );
}
