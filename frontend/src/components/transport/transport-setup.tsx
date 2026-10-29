"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition, type ReactNode } from "react";
import { ArrowDown, ArrowUp, Plus, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { StaffLookupItem } from "@/lib/api/types";
import { runAction } from "@/lib/client/action";
import { saveBus, saveQuestions, saveRuns } from "@/lib/transport/admin-actions";
import type { Bus, CheckQuestion, Run } from "@/lib/transport/transport";
import { FormDialog } from "@/components/ui/dialog";
import { Button, Card, Field, Input, Select, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

/** Add or edit a bus: name, plate, seats and its usual driver. Retire a bus by switching it off. */
export function BusDialog({ bus, staff, trigger }: { bus: Bus | null; staff: StaffLookupItem[]; trigger: ReactNode }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [plate, setPlate] = useState("");
  const [capacity, setCapacity] = useState("");
  const [driver, setDriver] = useState("");
  const [active, setActive] = useState(true);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const server = problem?.errors ?? {};
  const local = {
    name: !name.trim() ? "Give the bus a name, e.g. Bus 1." : name.length > 60 ? "60 characters at most." : undefined,
    plate: plate.length > 20 ? "20 characters at most." : undefined,
    capacity: /^\d{1,3}$/.test(capacity) && Number(capacity) >= 1 ? undefined : "Seats, e.g. 18.",
  };
  const errors = { name: local.name ?? server.name?.[0], plate: local.plate ?? server.plateNumber?.[0], capacity: local.capacity ?? server.capacity?.[0], driver: server.driverStaffId?.[0] };
  const id = bus ? `bus-${bus.id}` : "bus-new";

  const onOpenChange = (v: boolean) => {
    if (v) {
      setName(bus?.name ?? "");
      setPlate(bus?.plateNumber ?? "");
      setCapacity(bus ? String(bus.capacity) : "");
      setDriver(bus?.driverStaffId ?? "");
      setActive(bus?.isActive ?? true);
      setProblem(null);
    }
    setOpen(v);
  };
  // Keep a driver who has since left in the list, so editing doesn't silently unassign them.
  const options = bus?.driverStaffId && !staff.some((s) => s.id === bus.driverStaffId) ? [{ id: bus.driverStaffId, fullName: `${bus.driverName ?? "Unknown"} (inactive)`, jobTitle: null }, ...staff] : staff;

  return (
    <FormDialog title={bus ? `Edit ${bus.name}` : "Add a bus"} trigger={trigger} open={open} onOpenChange={onOpenChange}>
      {(close) => (
        <div className="space-y-4 p-5">
          {problem && !problem.errors && (
            <Alert tone="danger" title="Couldn't save">
              {problemMessage(problem)}
            </Alert>
          )}
          <Field id={`${id}-name`} label="Name" error={errors.name}>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Bus 1" {...describedBy(`${id}-name`, errors.name)} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id={`${id}-plate`} label="Plate number (optional)" error={errors.plate}>
              <Input value={plate} onChange={(e) => setPlate(e.target.value.toUpperCase())} placeholder="ABC 123 XY" {...describedBy(`${id}-plate`, errors.plate)} />
            </Field>
            <Field id={`${id}-cap`} label="Seats" error={errors.capacity}>
              <Input value={capacity} onChange={(e) => setCapacity(e.target.value)} inputMode="numeric" className="tabular" {...describedBy(`${id}-cap`, errors.capacity)} />
            </Field>
          </div>
          <Field id={`${id}-driver`} label="Driver" error={errors.driver} hint="Give the driver the Driver role in Staff so they can sign in to the bus page.">
            <Select value={driver} onChange={(e) => setDriver(e.target.value)} className="w-full" {...describedBy(`${id}-driver`, errors.driver, true)}>
              <option value="">No driver yet</option>
              {options.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.jobTitle ? `${s.fullName} · ${s.jobTitle}` : s.fullName}
                </option>
              ))}
            </Select>
          </Field>
          {bus && (
            <span className="flex items-center gap-3 text-sm">
              <Switch checked={active} onCheckedChange={setActive} label="Bus in service" />
              <span aria-hidden>{active ? "In service" : "Retired (kept for reports)"}</span>
            </span>
          )}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Boolean(local.name || local.plate || local.capacity)}
              onClick={async () => {
                setProblem(null);
                setBusy(true);
                const r = await runAction(
                  () => saveBus(bus?.id ?? null, { name: name.trim(), plateNumber: plate.trim() || null, capacity: Number(capacity), driverStaffId: driver || null, isActive: active }),
                  { success: bus ? "Bus saved" : "Bus added", onProblem: setProblem },
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

type RunRow = { id: string | null; name: string; kind: Run["kind"]; isActive: boolean };

/** The runs each bus makes, in order. Rename, reorder, add, or retire (never delete: history keeps them). */
export function RunsEditor({ initial }: { initial: Run[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const start = () => [...initial].sort((a, b) => a.order - b.order).map((r) => ({ id: r.id, name: r.name, kind: r.kind, isActive: r.isActive }));
  const [rows, setRows] = useState<RunRow[]>(start);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const bad = rows.some((r) => !r.name.trim() || r.name.length > 80);
  const dirty = JSON.stringify(rows) !== JSON.stringify(start());
  const set = (i: number, p: Partial<RunRow>) => setRows((rs) => rs.map((r, j) => (j === i ? { ...r, ...p } : r)));
  const move = (i: number, d: -1 | 1) => setRows((rs) => {
    const next = [...rs];
    [next[i], next[i + d]] = [next[i + d], next[i]];
    return next;
  });

  return (
    <Card className="space-y-4 p-5">
      <div>
        <h2 className="font-display text-lg font-semibold">Runs</h2>
        <p className="text-sm text-muted">Each bus makes these runs every school day, in this order. Switch one off to retire it; past trips keep it.</p>
      </div>
      {problem && (
        <Alert tone="danger" title="Couldn't save">
          {problemMessage(problem)}
        </Alert>
      )}
      <ol className="space-y-2">
        {rows.map((r, i) => (
          <li key={r.id ?? `new-${i}`} className={cn("flex flex-wrap items-center gap-2 rounded-input border border-border p-2", !r.isActive && "opacity-60")}>
            <span className="flex flex-col">
              <button type="button" className="rounded p-0.5 text-muted hover:text-text disabled:opacity-30" disabled={i === 0} onClick={() => move(i, -1)} aria-label={`Move ${r.name || "run"} up`}>
                <ArrowUp className="size-3.5" aria-hidden />
              </button>
              <button type="button" className="rounded p-0.5 text-muted hover:text-text disabled:opacity-30" disabled={i === rows.length - 1} onClick={() => move(i, 1)} aria-label={`Move ${r.name || "run"} down`}>
                <ArrowDown className="size-3.5" aria-hidden />
              </button>
            </span>
            <label className="sr-only" htmlFor={`run-${i}`}>
              Run name
            </label>
            <Input id={`run-${i}`} value={r.name} onChange={(e) => set(i, { name: e.target.value })} className={cn("w-auto min-w-0 flex-1", !r.name.trim() && "border-danger")} />
            <label className="sr-only" htmlFor={`run-kind-${i}`}>
              Kind
            </label>
            <Select id={`run-kind-${i}`} className="w-32" value={r.kind} onChange={(e) => set(i, { kind: e.target.value as Run["kind"] })}>
              <option value="Pickup">Pickup</option>
              <option value="DropOff">Drop-off</option>
            </Select>
            <Switch checked={r.isActive} onCheckedChange={(v) => set(i, { isActive: v })} label={`${r.name || "Run"} in use`} />
          </li>
        ))}
      </ol>
      <div className="flex flex-wrap justify-between gap-2">
        <Button variant="secondary" size="sm" onClick={() => setRows((rs) => [...rs, { id: null, name: "", kind: "Pickup", isActive: true }])}>
          <Plus className="size-4" aria-hidden />
          Add a run
        </Button>
        <Button
          loading={busy}
          loadingText="Saving…"
          disabled={bad || !dirty}
          onClick={async () => {
            setProblem(null);
            setBusy(true);
            const r = await runAction(() => saveRuns(rows.map((row, i) => ({ id: row.id, name: row.name.trim(), kind: row.kind, order: i + 1, isActive: row.isActive }))), {
              success: "Runs saved",
              onProblem: setProblem,
            });
            setBusy(false);
            if (r) startTransition(() => router.refresh());
          }}
        >
          <Save className="size-4" aria-hidden />
          Save runs
        </Button>
      </div>
    </Card>
  );
}

type QuestionRow = { id: string | null; text: string; kind: CheckQuestion["kind"]; isActive: boolean; expectedYes: boolean; flagIfAnswered: boolean };

/** The driver's daily checklist. A yes/no question flags the day when the answer isn't the expected one. */
export function QuestionsEditor({ initial }: { initial: CheckQuestion[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const start = () =>
    [...initial].sort((a, b) => a.order - b.order).map((q) => ({ id: q.id, text: q.text, kind: q.kind, isActive: q.isActive, expectedYes: q.expectedYes, flagIfAnswered: q.flagIfAnswered }));
  const [rows, setRows] = useState<QuestionRow[]>(start);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const bad = rows.some((r) => !r.text.trim() || r.text.length > 200);
  const dirty = JSON.stringify(rows) !== JSON.stringify(start());
  const set = (i: number, p: Partial<QuestionRow>) => setRows((rs) => rs.map((r, j) => (j === i ? { ...r, ...p } : r)));
  const move = (i: number, d: -1 | 1) => setRows((rs) => {
    const next = [...rs];
    [next[i], next[i + d]] = [next[i + d], next[i]];
    return next;
  });

  return (
    <Card className="space-y-4 p-5">
      <div>
        <h2 className="font-display text-lg font-semibold">Daily checklist</h2>
        <p className="text-sm text-muted">Drivers answer these each morning. A wrong yes/no answer, or anything written where you choose, flags the day and notifies admins.</p>
      </div>
      {problem && (
        <Alert tone="danger" title="Couldn't save">
          {problemMessage(problem)}
        </Alert>
      )}
      <ol className="space-y-2">
        {rows.map((q, i) => (
          <li key={q.id ?? `new-${i}`} className={cn("space-y-2 rounded-input border border-border p-3", !q.isActive && "opacity-60")}>
            <div className="flex items-center gap-2">
              <span className="flex flex-col">
                <button type="button" className="rounded p-0.5 text-muted hover:text-text disabled:opacity-30" disabled={i === 0} onClick={() => move(i, -1)} aria-label="Move up">
                  <ArrowUp className="size-3.5" aria-hidden />
                </button>
                <button type="button" className="rounded p-0.5 text-muted hover:text-text disabled:opacity-30" disabled={i === rows.length - 1} onClick={() => move(i, 1)} aria-label="Move down">
                  <ArrowDown className="size-3.5" aria-hidden />
                </button>
              </span>
              <label className="sr-only" htmlFor={`q-${i}`}>
                Question
              </label>
              <Input id={`q-${i}`} value={q.text} onChange={(e) => set(i, { text: e.target.value })} className={cn("w-auto min-w-0 flex-1", !q.text.trim() && "border-danger")} />
              <Switch checked={q.isActive} onCheckedChange={(v) => set(i, { isActive: v })} label="Question in use" />
            </div>
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2 pl-7 text-sm">
              <span className="flex items-center gap-2">
                <label htmlFor={`q-kind-${i}`} className="text-muted">
                  Answer
                </label>
                <Select id={`q-kind-${i}`} className="w-32" value={q.kind} onChange={(e) => set(i, { kind: e.target.value as QuestionRow["kind"] })}>
                  <option value="YesNo">Yes / No</option>
                  <option value="Number">Number</option>
                  <option value="Text">Text</option>
                </Select>
              </span>
              {q.kind === "YesNo" ? (
                <span className="flex items-center gap-2">
                  <label htmlFor={`q-good-${i}`} className="text-muted">
                    Good answer
                  </label>
                  <Select id={`q-good-${i}`} className="w-24" value={q.expectedYes ? "yes" : "no"} onChange={(e) => set(i, { expectedYes: e.target.value === "yes" })}>
                    <option value="yes">Yes</option>
                    <option value="no">No</option>
                  </Select>
                </span>
              ) : (
                <label className="flex items-center gap-2">
                  <input type="checkbox" className="size-4 accent-brand" checked={q.flagIfAnswered} onChange={(e) => set(i, { flagIfAnswered: e.target.checked })} />
                  Flag the day if anything is entered
                </label>
              )}
            </div>
          </li>
        ))}
      </ol>
      <div className="flex flex-wrap justify-between gap-2">
        <Button variant="secondary" size="sm" onClick={() => setRows((rs) => [...rs, { id: null, text: "", kind: "YesNo", isActive: true, expectedYes: true, flagIfAnswered: false }])}>
          <Plus className="size-4" aria-hidden />
          Add a question
        </Button>
        <Button
          loading={busy}
          loadingText="Saving…"
          disabled={bad || !dirty}
          onClick={async () => {
            setProblem(null);
            setBusy(true);
            const r = await runAction(
              () => saveQuestions(rows.map((q, i) => ({ id: q.id, text: q.text.trim(), kind: q.kind, order: i + 1, isActive: q.isActive, expectedYes: q.expectedYes, flagIfAnswered: q.kind !== "YesNo" && q.flagIfAnswered }))),
              { success: "Checklist saved", onProblem: setProblem },
            );
            setBusy(false);
            if (r) startTransition(() => router.refresh());
          }}
        >
          <Save className="size-4" aria-hidden />
          Save checklist
        </Button>
      </div>
    </Card>
  );
}
