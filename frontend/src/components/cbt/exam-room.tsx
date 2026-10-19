"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { ArrowLeft, ArrowRight, Check, CheckCircle2, Clock, CloudOff, Loader2, Send } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { logFocus, saveAnswer, startAttempt, submitAttempt } from "@/lib/cbt/student-actions";
import {
  AUTOSAVE_MS,
  MAX_ANSWER_TEXT,
  answerBody,
  answerOf,
  attemptImageUrl,
  formatRemaining,
  isAnswered,
  listNumbers,
  timerAnnouncement,
  timerTone,
  unanswered,
  type Answer,
  type AttemptPaper,
  type PaperItem,
  type StudentTest,
} from "@/lib/cbt/attempt";
import { splitGap } from "@/lib/cbt/questions";
import { formatMarks } from "@/lib/cbt/tests";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Card, Input, buttonClasses, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand";

type Phase = "intro" | "starting" | "sitting" | "done";
type SaveState = "saving" | "saved" | "error";
const RETRY_MS = 3000;

/** Calls an action, turning a thrown network failure into a Problem like every other failure. */
async function call<T>(fn: () => Promise<{ ok: true; data: T } | { ok: false; problem: Problem }>): Promise<{ ok: true; data: T } | { ok: false; problem: Problem }> {
  try {
    return await fn();
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
}
const transient = (p: Problem) => p.status === 0 || p.status === 429 || p.status >= 500;

/**
 * E16.3: one student's sitting. The server owns the clock (deadline and remainingSeconds) and the
 * session: opening the test elsewhere takes it over, and this copy then asks before taking it back.
 * Answers save one question at a time, ~800 ms after the last change, and retry on network errors.
 */
export function ExamRoom({ test }: { test: StudentTest }) {
  const router = useRouter();
  const [phase, setPhase] = useState<Phase>("intro");
  const [startProblem, setStartProblem] = useState<Problem | null>(null);
  const [paper, setPaper] = useState<AttemptPaper | null>(null);
  const [answers, setAnswers] = useState<Record<string, Answer>>({});
  const [saves, setSaves] = useState<Record<string, SaveState>>({});
  const [current, setCurrent] = useState(0);
  const [endsAt, setEndsAt] = useState(0);
  const [now, setNow] = useState(0);
  const [announcement, setAnnouncement] = useState("");
  const [takenOver, setTakenOver] = useState(false);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<{ autoSubmitted: boolean; problem?: Problem } | null>(null);

  const token = useRef<string | null>(null);
  const answersRef = useRef(answers);
  const dirty = useRef(new Set<string>());
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const inflight = useRef(new Map<string, Promise<void>>());
  const stopped = useRef(false);
  const lastFocus = useRef<"Blur" | "Focus">("Focus");
  const remainingRef = useRef(Number.POSITIVE_INFINITY);
  const handInRef = useRef<(auto: boolean) => Promise<void>>(async () => {});
  const runSaveRef = useRef<(itemId: string) => Promise<void>>(async () => {});

  const finish = useCallback((autoSubmitted: boolean, problem?: Problem) => {
    stopped.current = true;
    for (const t of timers.current.values()) clearTimeout(t);
    timers.current.clear();
    setResult({ autoSubmitted, problem });
    setPhase("done");
  }, []);

  const begin = useCallback((p: AttemptPaper) => {
    token.current = p.sessionToken;
    if (p.status === "Submitted") {
      setPaper(p);
      finish(p.autoSubmitted);
      return;
    }
    // Keep anything typed here that hasn't reached the server; take the rest from the paper.
    const merged: Record<string, Answer> = {};
    for (const item of p.items) merged[item.id] = dirty.current.has(item.id) && answersRef.current[item.id] ? answersRef.current[item.id] : answerOf(item);
    answersRef.current = merged;
    setAnswers(merged);
    setPaper(p);
    const t = Date.now();
    setEndsAt(t + p.remainingSeconds * 1000);
    setNow(t);
    remainingRef.current = p.remainingSeconds;
    stopped.current = false;
    setTakenOver(false);
    setPhase("sitting");
  }, [finish]);

  const save = useCallback(
    async (itemId: string): Promise<void> => {
      if (stopped.current || !paper || !token.current) return;
      const item = paper.items.find((i) => i.id === itemId);
      if (!item) return;
      const answer = answersRef.current[itemId];
      setSaves((s) => ({ ...s, [itemId]: "saving" }));
      const r = await call(() => saveAnswer(paper.id, itemId, token.current!, answerBody(item.kind, answer)));
      if (r.ok) {
        // Only clear if nothing changed while the request was out.
        if (answersRef.current[itemId] === answer) dirty.current.delete(itemId);
        setSaves((s) => ({ ...s, [itemId]: dirty.current.has(itemId) ? "saving" : "saved" }));
        setEndsAt(Date.now() + r.data.remainingSeconds * 1000);
        return;
      }
      const p = r.problem;
      if (p.code === "attempt.other_session") {
        stopped.current = true;
        setSaves((s) => ({ ...s, [itemId]: "error" }));
        setTakenOver(true);
      } else if (p.code === "attempt.submitted" || p.code === "attempt.time_up") finish(true);
      else {
        setSaves((s) => ({ ...s, [itemId]: "error" }));
        if (transient(p)) timers.current.set(itemId, setTimeout(() => void runSaveRef.current(itemId), RETRY_MS));
      }
    },
    [paper, finish],
  );

  // One request per item at a time; a change during a save is sent right after it.
  const runSave = useCallback(
    (itemId: string): Promise<void> => {
      const prior = inflight.current.get(itemId) ?? Promise.resolve();
      const next = prior.then(() => (dirty.current.has(itemId) ? save(itemId) : undefined));
      inflight.current.set(itemId, next);
      return next;
    },
    [save],
  );

  useEffect(() => {
    runSaveRef.current = runSave;
  }, [runSave]);

  const setAnswer = (item: PaperItem, answer: Answer) => {
    const next = { ...answersRef.current, [item.id]: answer };
    answersRef.current = next;
    setAnswers(next);
    dirty.current.add(item.id);
    setSaves((s) => ({ ...s, [item.id]: "saving" }));
    clearTimeout(timers.current.get(item.id));
    timers.current.set(item.id, setTimeout(() => void runSave(item.id), AUTOSAVE_MS));
  };

  const flush = async () => {
    for (const [id, t] of timers.current) {
      clearTimeout(t);
      timers.current.delete(id);
    }
    await Promise.all([...dirty.current].map((id) => runSave(id)));
  };

  const start = async () => {
    setStartProblem(null);
    // A takeover restarts from the paper screen; only the first start shows the starting state.
    if (phase !== "sitting") setPhase("starting");
    for (let attempt = 0; attempt < 4; attempt++) {
      const r = await call(() => startAttempt(test.id));
      if (r.ok) return begin(r.data);
      // A double click or a refresh mid-start: the first request is still creating the paper.
      if (r.problem.code === "attempt.starting" || transient(r.problem)) {
        await new Promise((res) => setTimeout(res, 1500));
        continue;
      }
      setStartProblem(r.problem);
      setPhase(takenOver ? "sitting" : "intro");
      return;
    }
    setStartProblem(unreachableProblem());
    setPhase(takenOver ? "sitting" : "intro");
  };

  const handIn = async (auto: boolean) => {
    if (!paper || submitting || phase !== "sitting") return;
    setSubmitting(true);
    await flush();
    if (stopped.current && !auto) {
      setSubmitting(false);
      return;
    }
    const r = await call(() => submitAttempt(paper.id, token.current ?? ""));
    setSubmitting(false);
    if (r.ok) return finish(r.data.autoSubmitted || auto);
    const p = r.problem;
    if (p.code === "attempt.submitted") return finish(true);
    if (p.code === "attempt.other_session") {
      stopped.current = true;
      setTakenOver(true);
      return;
    }
    if (auto) {
      // The server hands it in itself 15 s after the deadline; show that rather than an error.
      finish(true, transient(p) ? undefined : p);
      return;
    }
    setStartProblem(p);
  };
  useEffect(() => {
    handInRef.current = handIn;
  });

  // The countdown: local ticks against the server's deadline, re-anchored by every save.
  useEffect(() => {
    if (phase !== "sitting") return;
    const tick = () => {
      const t = Date.now();
      setNow(t);
      const remaining = (endsAt - t) / 1000;
      const said = timerAnnouncement(remainingRef.current, remaining);
      if (said) setAnnouncement(said);
      remainingRef.current = remaining;
      if (remaining <= 0 && !stopped.current) void handInRef.current(true);
    };
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, [phase, endsAt]);

  // The focus log, and a warning before leaving with unsaved answers.
  useEffect(() => {
    if (phase !== "sitting" || !paper) return;
    const send = (kind: "Blur" | "Focus") => {
      if (lastFocus.current === kind || stopped.current || !token.current) return;
      lastFocus.current = kind;
      void call(() => logFocus(paper.id, token.current!, kind)).then((r) => {
        if (!r.ok && r.problem.code === "attempt.other_session") {
          stopped.current = true;
          setTakenOver(true);
        }
      });
    };
    const onVisibility = () => send(document.visibilityState === "hidden" ? "Blur" : "Focus");
    const onBlur = () => send("Blur");
    const onFocus = () => send("Focus");
    const onUnload = (e: BeforeUnloadEvent) => {
      if (dirty.current.size > 0) e.preventDefault();
    };
    document.addEventListener("visibilitychange", onVisibility);
    window.addEventListener("blur", onBlur);
    window.addEventListener("focus", onFocus);
    window.addEventListener("beforeunload", onUnload);
    return () => {
      document.removeEventListener("visibilitychange", onVisibility);
      window.removeEventListener("blur", onBlur);
      window.removeEventListener("focus", onFocus);
      window.removeEventListener("beforeunload", onUnload);
    };
  }, [phase, paper]);

  useEffect(() => {
    const pending = timers.current;
    return () => {
      for (const t of pending.values()) clearTimeout(t);
    };
  }, []);

  /* ------------------------------------------------------------------------------- screens */

  if (phase === "done") {
    return (
      <main className="mx-auto max-w-xl px-4 py-12">
        <Card className="space-y-4 p-6 text-center">
          <CheckCircle2 className="mx-auto size-12 text-success" aria-hidden />
          <h1 className="font-display text-2xl font-semibold">Handed in</h1>
          <p>Your teacher will release the results.</p>
          {result?.autoSubmitted && <p className="text-sm text-muted">Time ran out, so your answers were handed in automatically.</p>}
          {result?.problem && (
            <Alert tone="warning" title="We couldn't confirm the hand-in">
              {problemMessage(result.problem)} Your saved answers are kept and the test is handed in when the time runs out.
            </Alert>
          )}
          <Link href="/portal/tests" className={buttonClasses("primary")}>
            Back to tests
          </Link>
        </Card>
      </main>
    );
  }

  if (phase === "intro" || phase === "starting" || !paper) {
    const resuming = test.state === "InProgress";
    return (
      <main className="mx-auto max-w-xl px-4 py-10">
        <Link href="/portal/tests" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
          <ArrowLeft className="size-4" aria-hidden />
          Tests
        </Link>
        <Card className="space-y-4 p-6">
          <div>
            <p className="text-sm text-muted">{test.subject}</p>
            <h1 className="font-display text-2xl font-semibold break-words">{test.title}</h1>
          </div>
          <ul className="space-y-1.5 text-sm">
            <li>
              <span className="tabular font-medium">{test.durationMinutes} minutes</span>, {test.questionCount} {test.questionCount === 1 ? "question" : "questions"}, {formatMarks(test.totalMarks)} marks.
            </li>
            <li>The timer starts when you press {resuming ? "Continue" : "Start"} and doesn&apos;t stop if you leave.</li>
            <li>Your answers save as you go. Don&apos;t close the tab or open the test anywhere else.</li>
            <li>When time runs out, your answers are handed in automatically.</li>
          </ul>
          {test.instructions && (
            <div className="rounded-input bg-surface-2 p-4 text-sm whitespace-pre-wrap">
              <p className="mb-1 font-medium">From your teacher</p>
              {test.instructions}
            </div>
          )}
          {startProblem && (
            <Alert tone="danger" title={resuming ? "Couldn't continue the test" : "Couldn't start the test"}>
              {problemMessage(startProblem)}
            </Alert>
          )}
          {test.state === "Open" || test.state === "InProgress" ? (
            <Button className="w-full" loading={phase === "starting"} loadingText={resuming ? "Opening…" : "Starting…"} onClick={() => void start()}>
              {resuming ? "Continue" : "Start the test"}
            </Button>
          ) : (
            <p className="text-sm text-muted">{test.state === "Upcoming" ? "This test isn't open yet." : "This test is closed."}</p>
          )}
        </Card>
      </main>
    );
  }

  const items = [...paper.items].sort((a, b) => a.order - b.order);
  const item = items[Math.min(current, items.length - 1)];
  const section = paper.sections.find((s) => s.index === item.section);
  const firstOfSection = items.findIndex((i) => i.section === item.section) === current;
  const answeredCount = items.filter((i) => isAnswered(i.kind, answers[i.id] ?? answerOf(i))).length;
  const remaining = Math.max(0, (endsAt - now) / 1000);
  const tone = timerTone(remaining);
  const states = Object.values(saves);
  const status = states.includes("error") ? "error" : states.includes("saving") ? "saving" : "saved";
  const left = unanswered(items, answers);
  const answer = answers[item.id] ?? answerOf(item);

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-20 border-b border-border bg-surface/95 backdrop-blur">
        <div className="mx-auto flex w-full max-w-4xl flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2.5">
          <p className="min-w-0 flex-1 truncate font-medium">{paper.title}</p>
          <p
            role="timer"
            aria-label={`Time left ${formatRemaining(remaining)}`}
            className={cn(
              "tabular inline-flex items-center gap-1.5 rounded-input px-2.5 py-1 font-mono text-lg font-semibold",
              tone === "danger" ? "bg-danger/10 text-danger" : tone === "warning" ? "bg-warning/15 text-warning" : "text-text",
            )}
          >
            <Clock className="size-4" aria-hidden />
            {formatRemaining(remaining)}
          </p>
          <div className="flex w-full items-center justify-between gap-3 text-sm sm:w-auto sm:justify-start">
            <span className="tabular text-muted">
              {answeredCount} of {items.length} answered
            </span>
            <span className="inline-flex items-center gap-1 text-xs" data-testid="save-status">
              {status === "error" ? (
                <>
                  <CloudOff className="size-3.5 text-danger" aria-hidden />
                  <span className="text-danger">Not saved yet, retrying</span>
                </>
              ) : status === "saving" ? (
                <>
                  <Loader2 className="size-3.5 animate-spin text-muted" aria-hidden />
                  <span className="text-muted">Saving…</span>
                </>
              ) : (
                <>
                  <Check className="size-3.5 text-success" aria-hidden />
                  <span className="text-muted">All answers saved</span>
                </>
              )}
            </span>
          </div>
        </div>
        <p className="sr-only" aria-live="polite">
          {announcement}
        </p>
      </header>

      <main className="mx-auto grid w-full max-w-4xl flex-1 gap-5 px-4 py-5 md:grid-cols-[1fr_14rem] md:items-start">
        <div className="space-y-4">
          {startProblem && (
            <Alert tone="danger" title="Couldn't hand in">
              {problemMessage(startProblem)}
            </Alert>
          )}
          {section && (
            <div>
              <h2 className="font-display text-lg font-semibold">{section.title}</h2>
              {firstOfSection && section.instructions && <p className="mt-1 text-sm whitespace-pre-wrap text-muted">{section.instructions}</p>}
            </div>
          )}
          <Card className="p-5">
            <QuestionView key={item.id} attemptId={paper.id} item={item} number={current + 1} answer={answer} onChange={(a) => setAnswer(item, a)} />
          </Card>
          <div className="flex items-center justify-between gap-2">
            <Button variant="secondary" disabled={current === 0} onClick={() => setCurrent((c) => c - 1)}>
              <ArrowLeft className="size-4" aria-hidden />
              Previous
            </Button>
            {current < items.length - 1 ? (
              <Button variant="secondary" onClick={() => setCurrent((c) => c + 1)}>
                Next
                <ArrowRight className="size-4" aria-hidden />
              </Button>
            ) : (
              <Button onClick={() => setConfirmSubmit(true)}>
                <Send className="size-4" aria-hidden />
                Hand in
              </Button>
            )}
          </div>
        </div>

        <aside className="space-y-3 md:sticky md:top-20">
          <nav aria-label="Questions">
            <ol className="grid grid-cols-6 gap-1.5 sm:grid-cols-8 md:grid-cols-5">
              {items.map((it, i) => {
                const done = isAnswered(it.kind, answers[it.id] ?? answerOf(it));
                return (
                  <li key={it.id}>
                    <button
                      type="button"
                      onClick={() => setCurrent(i)}
                      aria-current={i === current ? "step" : undefined}
                      aria-label={`Question ${i + 1}, ${done ? "answered" : "not answered"}`}
                      className={cn(
                        "tabular flex size-10 items-center justify-center rounded-input border text-sm",
                        done ? "border-brand bg-brand text-on-brand" : "border-border bg-surface",
                        i === current && "outline-2 outline-offset-2 outline-brand",
                      )}
                    >
                      {i + 1}
                    </button>
                  </li>
                );
              })}
            </ol>
          </nav>
          <Button className="w-full" variant={current === items.length - 1 ? "secondary" : "primary"} onClick={() => setConfirmSubmit(true)}>
            <Send className="size-4" aria-hidden />
            Hand in
          </Button>
        </aside>
      </main>

      <ConfirmDialog
        open={confirmSubmit}
        onOpenChange={setConfirmSubmit}
        title="Hand in your test?"
        confirmLabel="Hand in"
        pendingLabel="Handing in…"
        onConfirm={async () => {
          await handIn(false);
          return true;
        }}
      >
        {left.length === 0 ? (
          <p>You&apos;ve answered every question. You can&apos;t change your answers after handing in.</p>
        ) : (
          <p>
            {left.length === 1 ? "Question" : "Questions"} {listNumbers(left)} {left.length === 1 ? "isn't" : "aren't"} answered yet. You can&apos;t change your answers after handing in.
          </p>
        )}
      </ConfirmDialog>

      <ConfirmDialog
        open={takenOver}
        // Closing after "Continue here" is the takeover succeeding; closing otherwise means leaving.
        onOpenChange={(v) => {
          if (!v && stopped.current) router.push("/portal/tests");
        }}
        title="This test is open somewhere else"
        confirmLabel="Continue here"
        pendingLabel="Opening…"
        onConfirm={async () => {
          await start();
          await flush();
          return true;
        }}
      >
        <p>It was opened in another tab or on another device. Continue here to carry on with this copy; the other one stops saving.</p>
        <p className="mt-2">
          <Link href="/portal/tests" className="text-brand hover:underline">
            Back to tests instead
          </Link>
        </p>
      </ConfirmDialog>
    </div>
  );
}

/** One question, rendered for its kind. Text answers keep local state so typing stays smooth. */
function QuestionView({ attemptId, item, number, answer, onChange }: { attemptId: string; item: PaperItem; number: number; answer: Answer; onChange: (a: Answer) => void }) {
  const gap = item.kind === "FillInTheGap" ? splitGap(item.text) : null;
  const name = `q-${item.id}`;
  const optionIds = answer.optionIds ?? [];

  return (
    <fieldset className="space-y-4">
      <legend className="mb-2 flex w-full items-baseline justify-between gap-3 text-sm text-muted">
        <span>Question {number}</span>
        <span className="tabular">
          {formatMarks(item.marks)} {item.marks === 1 ? "mark" : "marks"}
        </span>
      </legend>
      {gap ? (
        <p className="text-lg leading-loose whitespace-pre-wrap">
          {gap[0]}
          <Input
            aria-label={`Answer for the gap in question ${number}`}
            value={answer.text ?? ""}
            onChange={(e) => onChange({ ...answer, text: e.target.value })}
            maxLength={200}
            autoComplete="off"
            spellCheck={false}
            className="mx-1 inline-block w-44 align-baseline"
          />
          {gap[1]}
        </p>
      ) : (
        <p className="text-lg whitespace-pre-wrap">{item.text}</p>
      )}
      {/* The private BFF picture; next/image adds nothing here. */}
      {item.imageUrl && <img src={attemptImageUrl(attemptId, item.id)} alt={`Illustration for question ${number}`} className="max-h-96 max-w-full rounded-input border border-border object-contain" />}

      {(item.kind === "Objective" || item.kind === "MultiSelect") && (
        <div className="space-y-2" role="group" aria-label={item.kind === "Objective" ? "Choose one answer" : "Choose every correct answer"}>
          <p className="text-sm text-muted">{item.kind === "Objective" ? "Choose one answer." : "Choose every correct answer."}</p>
          {item.options.map((o, i) => {
            const on = optionIds.includes(o.id);
            return (
              <label key={o.id} className={cn("flex min-h-12 cursor-pointer items-center gap-3 rounded-input border px-3 py-2", on ? "border-brand bg-brand/5" : "border-border hover:bg-surface-2")}>
                <input
                  type={item.kind === "Objective" ? "radio" : "checkbox"}
                  name={name}
                  checked={on}
                  onChange={(e) =>
                    onChange({ ...answer, optionIds: item.kind === "Objective" ? [o.id] : e.target.checked ? [...optionIds, o.id] : optionIds.filter((x) => x !== o.id) })
                  }
                  className="size-5 shrink-0 accent-[var(--brand)]"
                />
                <span className="font-medium text-muted">{String.fromCharCode(65 + i)}.</span>
                <span className="break-words">{o.text}</span>
              </label>
            );
          })}
        </div>
      )}
      {item.kind === "TrueFalse" && (
        <div className="grid grid-cols-2 gap-3" role="radiogroup" aria-label="True or false">
          {[true, false].map((v) => (
            <label key={String(v)} className={cn("flex min-h-12 cursor-pointer items-center justify-center gap-2 rounded-input border px-3 font-medium", answer.trueFalse === v ? "border-brand bg-brand/5 text-brand" : "border-border hover:bg-surface-2")}>
              <input type="radio" name={name} checked={answer.trueFalse === v} onChange={() => onChange({ ...answer, trueFalse: v })} className="size-5 accent-[var(--brand)]" />
              {v ? "True" : "False"}
            </label>
          ))}
        </div>
      )}
      {item.kind === "FillInTheGap" && !gap && (
        <Input aria-label={`Answer for question ${number}`} value={answer.text ?? ""} onChange={(e) => onChange({ ...answer, text: e.target.value })} maxLength={200} autoComplete="off" />
      )}
      {item.kind === "ShortAnswer" && (
        <Input aria-label={`Answer for question ${number}`} value={answer.text ?? ""} onChange={(e) => onChange({ ...answer, text: e.target.value })} maxLength={MAX_ANSWER_TEXT} autoComplete="off" placeholder="Type your answer" />
      )}
      {item.kind === "Theory" && (
        <div>
          <textarea
            aria-label={`Answer for question ${number}`}
            value={answer.text ?? ""}
            onChange={(e) => onChange({ ...answer, text: e.target.value })}
            maxLength={MAX_ANSWER_TEXT}
            rows={10}
            placeholder="Write your answer"
            className={textareaClass}
          />
          <p className="tabular mt-1 text-right text-xs text-muted">
            {(answer.text ?? "").length} / {MAX_ANSWER_TEXT}
          </p>
        </div>
      )}
    </fieldset>
  );
}
