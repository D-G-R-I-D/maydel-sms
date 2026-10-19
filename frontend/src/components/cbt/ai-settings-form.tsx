"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { saveAiSettings, setAiAllowance } from "@/lib/cbt/actions";
import type { AiSettings, AiUsageRow } from "@/lib/cbt/ai";
import { runAction } from "@/lib/client/action";
import { Button, Card, Field, Input } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

const whole = (v: string, max: number) => /^\d+$/.test(v.trim()) && Number(v) <= max;

/** E16.9 (admin): the on/off switch, the default monthly allowance per teacher and the school cap. */
export function AiSettingsForm({ initial }: { initial: AiSettings }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [enabled, setEnabled] = useState(initial.enabled);
  const [perTeacher, setPerTeacher] = useState(String(initial.defaultMonthlyGenerations));
  const [school, setSchool] = useState(String(initial.schoolMonthlyGenerations));
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const perError = whole(perTeacher, 1000) ? null : "A whole number from 0 to 1000.";
  const schoolError = whole(school, 100000) ? null : "A whole number from 0 to 100,000.";

  return (
    <Card className="space-y-4 p-5">
      {!initial.available && (
        <Alert tone="warning" title="AI isn't set up on this server">
          No AI key is configured, so teachers can't generate questions yet even when this is switched on.
        </Alert>
      )}
      {problem && (
        <Alert tone="danger" title="Couldn't save">
          {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
        </Alert>
      )}
      <span className="flex items-center gap-3 text-sm">
        <Switch checked={enabled} onCheckedChange={setEnabled} label="AI question writing" />
        <span aria-hidden>{enabled ? "On: teachers can write questions with AI" : "Off for everyone"}</span>
      </span>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="ai-default" label="Requests per teacher per month" error={perError ?? undefined} hint="Each successful request or regenerate uses one. Override it per teacher below.">
          <Input id="ai-default" value={perTeacher} onChange={(e) => setPerTeacher(e.target.value)} inputMode="numeric" className="tabular" />
        </Field>
        <Field id="ai-school" label="School limit per month" error={schoolError ?? undefined} hint="All teachers together. 0 means no limit beyond each teacher's own.">
          <Input id="ai-school" value={school} onChange={(e) => setSchool(e.target.value)} inputMode="numeric" className="tabular" />
        </Field>
      </div>
      <div className="flex justify-end">
        <Button
          loading={busy}
          loadingText="Saving…"
          disabled={Boolean(perError || schoolError)}
          onClick={async () => {
            setProblem(null);
            setBusy(true);
            const r = await runAction(() => saveAiSettings({ enabled, defaultMonthlyGenerations: Number(perTeacher), schoolMonthlyGenerations: Number(school) }), { success: "AI settings saved", onProblem: setProblem });
            setBusy(false);
            if (r) startTransition(() => router.refresh());
          }}
        >
          <Save className="size-4" aria-hidden />
          Save
        </Button>
      </div>
    </Card>
  );
}

/** One teacher's allowance override: empty = the school default. Saves on leaving the box. */
export function AllowanceInput({ row, defaultAllowance }: { row: AiUsageRow; defaultAllowance: number }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const initial = row.customAllowance ? String(row.allowance) : "";
  const [value, setValue] = useState(initial);
  const [saved, setSaved] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const id = `allow-${row.userId}`;

  // Reads the box itself: a blur can fire before the last keystroke's re-render.
  const save = async (value: string) => {
    if (value.trim() === saved.trim()) return;
    if (value.trim() !== "" && !whole(value, 1000)) {
      setError("0 to 1000, or empty for the default.");
      return;
    }
    setError(null);
    const r = await runAction(() => setAiAllowance(row.userId, value.trim() === "" ? null : Number(value)), {
      success: value.trim() === "" ? `${row.name} is back on the default` : `${row.name} can make ${value} requests a month`,
      onProblem: (p) => setError(problemMessage(p)),
    });
    if (r) {
      setSaved(value);
      startTransition(() => router.refresh());
    }
  };

  return (
    <div>
      <label htmlFor={id} className="sr-only">
        Monthly allowance for {row.name}
      </label>
      <Input
        id={id}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onBlur={(e) => void save(e.currentTarget.value)}
        onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), void save(e.currentTarget.value))}
        placeholder={`Default (${defaultAllowance})`}
        inputMode="numeric"
        className="tabular w-32"
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
      />
      {error && (
        <p id={`${id}-error`} className="mt-1 text-xs text-danger" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
