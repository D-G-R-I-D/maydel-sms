"use client";

import { useState } from "react";
import { BellRing, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { saveFeeReminders } from "@/lib/finance/actions";
import { runAction } from "@/lib/client/action";
import { Button, Card, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

type Reminders = { enabled: boolean; intervalDays: number };

/** E12.8: overdue-fee reminders to guardians (portal notification and email), on a schedule. */
export function FeeRemindersForm({ initial, canEdit }: { initial: Reminders; canEdit: boolean }) {
  const [saved, setSaved] = useState(initial);
  const [enabled, setEnabled] = useState(initial.enabled);
  const [days, setDays] = useState(String(initial.intervalDays));
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const n = Number(days);
  const local = /^\d+$/.test(days) && n >= 1 && n <= 60 ? undefined : "1 to 60 days.";
  const error = local ?? problem?.errors?.intervalDays?.[0];
  const dirty = enabled !== saved.enabled || n !== saved.intervalDays;

  const save = async () => {
    setProblem(null);
    setBusy(true);
    const r = await runAction(() => saveFeeReminders({ enabled, intervalDays: n }), { success: enabled ? `Reminders on: every ${n} day${n === 1 ? "" : "s"}` : "Reminders off", onProblem: setProblem });
    setBusy(false);
    if (r) {
      setSaved(r);
      setDays(String(r.intervalDays));
    }
  };

  return (
    <Card className="space-y-4 p-5">
      <div className="flex items-start gap-3">
        <BellRing className="mt-0.5 size-5 shrink-0 text-brand" aria-hidden />
        <div>
          <h2 className="font-display text-lg font-semibold">Overdue reminders</h2>
          <p className="text-sm text-muted">
            When an instalment is past due, guardians get a portal notification and an email, sent between 8 am and 6 pm (WAT), then again every few days until it&apos;s paid. Every payment already sends a receipt.
          </p>
        </div>
      </div>
      {problem && !problem.errors && (
        <Alert tone="danger" title="Couldn't save">
          {problemMessage(problem)}
        </Alert>
      )}
      <span className="flex items-center gap-3 text-sm">
        <Switch checked={enabled} onCheckedChange={setEnabled} label="Send overdue reminders" disabled={!canEdit} />
        <span aria-hidden>{enabled ? "Reminders on" : "Reminders off"}</span>
      </span>
      {enabled && (
        <Field id="rem-days" label="Repeat every (days)" error={error} hint="Until the balance is paid.">
          <Input {...describedBy("rem-days", error, true)} value={days} onChange={(e) => setDays(e.target.value)} inputMode="numeric" className="tabular w-28" disabled={!canEdit} />
        </Field>
      )}
      {canEdit && (
        <div className="flex justify-end">
          <Button loading={busy} loadingText="Saving…" disabled={!dirty || Boolean(enabled && local)} onClick={save}>
            <Save className="size-4" aria-hidden />
            Save
          </Button>
        </div>
      )}
    </Card>
  );
}
