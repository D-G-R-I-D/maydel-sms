"use client";

import { useRouter } from "next/navigation";
import { useState, useSyncExternalStore, useTransition } from "react";
import { AlertTriangle, ArrowLeftFromLine, ArrowRightToLine, Bus as BusIcon, CheckCircle2, ClipboardCheck, RotateCcw } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { watClock, watTime } from "@/lib/clock/clock";
import { submitCheck, tapTrip, undoTrip } from "@/lib/transport/driver-actions";
import { draftAnswers, duration, flagsDay, missingAnswers, nextEvent, toAnswerInputs, tripState, type AnswerDraft, type BusDay, type CheckQuestion, type Trip } from "@/lib/transport/transport";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

const subscribeSecond = (cb: () => void) => {
  const id = window.setInterval(cb, 1000);
  return () => window.clearInterval(id);
};

/** A live WAT clock (seconds), rendered only in the browser. */
export function LiveClock() {
  const now = useSyncExternalStore(subscribeSecond, () => Math.floor(Date.now() / 1000), () => null);
  return <span className="tabular">{now == null ? "--:--:--" : watClock(new Date(now * 1000))}</span>;
}

/**
 * E18 driver's day on the phone: for each of their buses, the daily checklist and one row per run with
 * a big Left / Back button. A mis-tap can be undone for 2 minutes; after that the admin corrects it.
 */
export function DriverDay({ buses, questions }: { buses: BusDay[]; questions: CheckQuestion[] }) {
  return (
    <div className="space-y-6">
      {buses.map((b) => (
        <BusCard key={b.bus.id} day={b} questions={questions} />
      ))}
    </div>
  );
}

function BusCard({ day, questions }: { day: BusDay; questions: CheckQuestion[] }) {
  return (
    <section aria-labelledby={`bus-${day.bus.id}`} className="space-y-3">
      <h2 id={`bus-${day.bus.id}`} className="flex items-center gap-2 font-display text-xl font-semibold">
        <BusIcon className="size-5 text-brand" aria-hidden />
        {day.bus.name}
        {day.bus.plateNumber && <span className="font-sans text-sm font-normal text-muted">{day.bus.plateNumber}</span>}
      </h2>
      <Checklist busId={day.bus.id} questions={questions.filter((q) => q.isActive)} check={day.check} />
      <Card className="divide-y divide-border p-0">
        {day.trips.map((t) => (
          <TripRow key={t.runId} trip={t} />
        ))}
        {day.trips.length === 0 && <p className="p-4 text-sm text-muted">No runs are set up. Ask the school admin.</p>}
      </Card>
    </section>
  );
}

function TripRow({ trip }: { trip: Trip }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [busy, setBusy] = useState<"tap" | "undo" | null>(null);
  const next = nextEvent(trip);
  const state = tripState(trip);

  const tap = async () => {
    if (!next) return;
    setBusy("tap");
    const r = await runAction(() => tapTrip(trip.busId, trip.runId, next), { success: (t) => `${next === "Left" ? "Left" : "Back"} at ${watTime(next === "Left" ? t.leftAt : t.backAt)}` });
    setBusy(null);
    if (r) startTransition(() => router.refresh());
  };
  const undo = async () => {
    setBusy("undo");
    const r = await runAction(() => undoTrip(trip.busId, trip.runId), { success: "Undone" });
    setBusy(null);
    if (r) startTransition(() => router.refresh());
  };

  return (
    <div className="space-y-3 p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="font-medium">{trip.run}</p>
          <p className="tabular text-sm text-muted">
            Left {watTime(trip.leftAt)} · Back {watTime(trip.backAt)}
            {trip.minutes != null && <span> · {duration(trip.minutes)}</span>}
          </p>
          {trip.correction && <p className="mt-0.5 text-xs text-muted">Corrected by the office: {trip.correction}</p>}
        </div>
        <Badge tone={state === "back" ? "success" : state === "out" ? "warning" : "neutral"}>{state === "back" ? "Back" : state === "out" ? "On the road" : "Not left"}</Badge>
      </div>
      {next && (
        <Button size="lg" className="w-full text-lg" variant={next === "Left" ? "primary" : "secondary"} loading={busy === "tap"} loadingText="Saving…" onClick={() => void tap()}>
          {next === "Left" ? <ArrowRightToLine className="size-5" aria-hidden /> : <ArrowLeftFromLine className="size-5" aria-hidden />}
          {next === "Left" ? "Left school" : "Back at school"}
        </Button>
      )}
      {trip.canUndo && (
        <Button size="sm" variant="ghost" loading={busy === "undo"} loadingText="Undoing…" onClick={() => void undo()}>
          <RotateCcw className="size-4" aria-hidden />
          Undo last tap
        </Button>
      )}
    </div>
  );
}

function Checklist({ busId, questions, check }: { busId: string; questions: CheckQuestion[]; check: BusDay["check"] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(!check);
  const [drafts, setDrafts] = useState<Record<string, AnswerDraft>>(() => draftAnswers(questions, check));
  const [notes, setNotes] = useState(check?.notes ?? "");
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const [tried, setTried] = useState(false);
  const missing = missingAnswers(questions, drafts);
  const set = (id: string, d: Partial<AnswerDraft>) => setDrafts((all) => ({ ...all, [id]: { ...all[id], ...d } }));

  if (questions.length === 0) return null;

  if (!open && check) {
    return (
      <Card className={cn("flex items-center gap-3 p-4", check.isFlagged ? "border-warning/50 bg-warning/5" : "border-success/40 bg-success/5")}>
        {check.isFlagged ? <AlertTriangle className="size-6 shrink-0 text-warning" aria-hidden /> : <CheckCircle2 className="size-6 shrink-0 text-success" aria-hidden />}
        <div className="min-w-0 flex-1">
          <p className="font-medium">Checklist done at {watTime(check.submittedAt)}</p>
          <p className="text-sm text-muted">{check.isFlagged ? "Something was flagged; the office has been told." : "All good."}</p>
        </div>
        <Button size="sm" variant="ghost" onClick={() => setOpen(true)}>
          Edit
        </Button>
      </Card>
    );
  }

  const save = async () => {
    setTried(true);
    if (missing.length) return;
    setProblem(null);
    setBusy(true);
    const r = await runAction(() => submitCheck(busId, toAnswerInputs(questions, drafts), notes.trim() || null), {
      success: (c) => (c.isFlagged ? "Checklist saved. The office has been told about the problem." : "Checklist saved"),
      onProblem: setProblem,
    });
    setBusy(false);
    if (r) {
      setOpen(false);
      startTransition(() => router.refresh());
    }
  };

  return (
    <Card className="space-y-4 p-4">
      <p className="flex items-center gap-2 font-medium">
        <ClipboardCheck className="size-5 text-brand" aria-hidden />
        Daily checklist
      </p>
      {problem && (
        <Alert tone="danger" title="Couldn't save">
          {problemMessage(problem)}
        </Alert>
      )}
      <ol className="space-y-4">
        {questions.map((q) => {
          const d = drafts[q.id];
          const isMissing = tried && missing.includes(q.id);
          const flag = flagsDay(q, d);
          return (
            <li key={q.id} className="space-y-2">
              <p className={cn("text-sm font-medium", isMissing && "text-danger")} id={`q-${q.id}`}>
                {q.text}
              </p>
              {q.kind === "YesNo" ? (
                <div className="grid grid-cols-2 gap-2" role="radiogroup" aria-labelledby={`q-${q.id}`}>
                  {[true, false].map((v) => (
                    <button
                      key={String(v)}
                      type="button"
                      role="radio"
                      aria-checked={d?.yes === v}
                      onClick={() => set(q.id, { yes: v })}
                      className={cn(
                        "min-h-12 rounded-input border text-base font-semibold",
                        d?.yes === v ? (v === q.expectedYes ? "border-success bg-success/10 text-success" : "border-warning bg-warning/10 text-warning") : "border-border bg-surface",
                      )}
                    >
                      {v ? "Yes" : "No"}
                    </button>
                  ))}
                </div>
              ) : q.kind === "Number" ? (
                <Input inputMode="decimal" aria-labelledby={`q-${q.id}`} value={d?.number ?? ""} onChange={(e) => set(q.id, { number: e.target.value })} className="tabular w-40" />
              ) : (
                <Input aria-labelledby={`q-${q.id}`} value={d?.text ?? ""} onChange={(e) => set(q.id, { text: e.target.value })} placeholder={q.flagIfAnswered ? "Leave empty if nothing happened" : ""} />
              )}
              {isMissing && <p className="text-xs text-danger">Answer this one.</p>}
              {flag && <p className="text-xs text-warning">This will be flagged to the office.</p>}
            </li>
          );
        })}
      </ol>
      <div>
        <label htmlFor={`notes-${busId}`} className="text-sm font-medium">
          Notes (optional)
        </label>
        <Input id={`notes-${busId}`} value={notes} onChange={(e) => setNotes(e.target.value)} className="mt-1 w-full" />
      </div>
      {tried && missing.length > 0 && (
        <p className="text-sm text-danger" role="alert">
          Answer every question above, then save.
        </p>
      )}
      <div className="flex gap-2">
        <Button size="lg" className="flex-1" loading={busy} loadingText="Saving…" onClick={() => void save()}>
          <CheckCircle2 className="size-5" aria-hidden />
          Save checklist
        </Button>
        {check && (
          <Button size="lg" variant="ghost" onClick={() => setOpen(false)}>
            Cancel
          </Button>
        )}
      </div>
    </Card>
  );
}
