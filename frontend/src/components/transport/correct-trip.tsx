"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Pencil, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { longDay } from "@/lib/clock/clock";
import { lagosIso, toLagosParts } from "@/lib/time/lagos";
import { correctTrip } from "@/lib/transport/admin-actions";
import type { Trip } from "@/lib/transport/transport";
import { FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

const time = (iso: string | null) => (iso ? toLagosParts(iso).time : "");

/** Admin: set a run's Left / Back times for a day (WAT), with a required reason that is kept. */
export function CorrectTripButton({ trip }: { trip: Trip }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [left, setLeft] = useState("");
  const [back, setBack] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const server = problem?.errors ?? {};
  const local = {
    left: !left && back ? "Add when it left as well." : undefined,
    back: left && back && back <= left ? "Back must be after it left." : undefined,
    note: note.trim() === "" ? "Say why the times are being changed." : note.length > 500 ? "500 characters at most." : undefined,
  };
  const errors = { left: local.left ?? server.leftAt?.[0], back: local.back ?? server.backAt?.[0], note: local.note ?? server.note?.[0] };
  const id = `fix-${trip.busId}-${trip.runId}`;

  const onOpenChange = (v: boolean) => {
    if (v) {
      setLeft(time(trip.leftAt));
      setBack(time(trip.backAt));
      setNote("");
      setProblem(null);
    }
    setOpen(v);
  };

  return (
    <FormDialog
      title={`Correct ${trip.run}`}
      description={`${trip.bus} · ${longDay(trip.date)} · Abuja time (WAT)`}
      open={open}
      onOpenChange={onOpenChange}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Correct ${trip.bus}, ${trip.run}`}>
          <Pencil className="size-3.5" aria-hidden />
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
            <Field id={`${id}-left`} label="Left school" error={errors.left} hint="Leave both empty to clear the run">
              <Input type="time" value={left} onChange={(e) => setLeft(e.target.value)} {...describedBy(`${id}-left`, errors.left, true)} />
            </Field>
            <Field id={`${id}-back`} label="Back at school" error={errors.back}>
              <Input type="time" value={back} onChange={(e) => setBack(e.target.value)} {...describedBy(`${id}-back`, errors.back)} />
            </Field>
          </div>
          <Field id={`${id}-note`} label="Reason" error={errors.note} hint="e.g. Driver forgot to tap; confirmed by phone. Kept with the trip.">
            <Input value={note} onChange={(e) => setNote(e.target.value)} {...describedBy(`${id}-note`, errors.note, true)} />
          </Field>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Boolean(local.left || local.back || local.note)}
              onClick={async () => {
                setProblem(null);
                setBusy(true);
                const r = await runAction(
                  () => correctTrip(trip.busId, trip.runId, trip.date, { leftAt: left ? lagosIso(trip.date, left) : null, backAt: back ? lagosIso(trip.date, back) : null, note: note.trim() }),
                  { success: "Trip corrected", onProblem: setProblem },
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
