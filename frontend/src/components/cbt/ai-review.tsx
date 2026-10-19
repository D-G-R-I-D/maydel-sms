"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CheckCircle2, CircleAlert, Plus, RefreshCw, Save, Trash2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { regenerateQuestions, saveAiDrafts } from "@/lib/cbt/actions";
import { MAX_REGENERATIONS, assessmentLabel, draftProblem, draftValues, quotaBlocker, toDraftInput, type AiGeneration } from "@/lib/cbt/ai";
import { bankHref, type QuestionFormValues } from "@/lib/cbt/questions";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { SchoolLoader } from "@/components/ui/school-loader";
import { DraftEditor } from "./draft-editor";

type Item = { key: string; include: boolean; values: QuestionFormValues; aiProblem: string | null; edited: boolean; own: boolean; serverError?: string };

let seq = 0;
const itemsOf = (g: AiGeneration, number: number): Item[] =>
  (g.versions.find((v) => v.number === number)?.questions ?? []).map((q) => ({ key: `i${++seq}`, include: true, values: draftValues(q.question), aiProblem: q.problem, edited: false, own: false }));

/**
 * E16.8 review: one tab per version (each version avoids the earlier ones), editable drafts with the AI's
 * problem note, "Add my own question", and Save selected to bank (all or nothing).
 */
export function AiReview({ initial, topics }: { initial: AiGeneration; topics: string[] }) {
  const latest = (g: AiGeneration) => Math.max(...g.versions.map((v) => v.number));
  const [gen, setGen] = useState(initial);
  const [active, setActive] = useState(latest(initial));
  const [drafts, setDrafts] = useState<Record<number, Item[]>>(() => ({ [latest(initial)]: itemsOf(initial, latest(initial)) }));
  const [regenerating, setRegenerating] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [saved, setSaved] = useState<number | null>(null);

  useEffect(() => {
    if (!regenerating) return;
    const started = Date.now();
    const id = setInterval(() => setElapsed(Math.round((Date.now() - started) / 1000)), 1000);
    return () => clearInterval(id);
  }, [regenerating]);

  const version = gen.versions.find((v) => v.number === active);
  const items = drafts[active] ?? [];
  const left = MAX_REGENERATIONS - gen.regenerations;
  const blocker = quotaBlocker(gen.quota);
  const selected = items.filter((i) => i.include);
  const invalid = selected.filter((i) => draftProblem(i.values) !== null);

  const open = (n: number) => {
    setActive(n);
    setSaved(null);
    setDrafts((d) => (d[n] ? d : { ...d, [n]: itemsOf(gen, n) }));
  };
  const update = (key: string, patch: Partial<Item>) => setDrafts((d) => ({ ...d, [active]: (d[active] ?? []).map((i) => (i.key === key ? { ...i, ...patch } : i)) }));

  const regenerate = async () => {
    setProblem(null);
    setRegenerating(true);
    const r = await runAction(() => regenerateQuestions(gen.id), { success: "A new version is ready", onProblem: setProblem });
    setRegenerating(false);
    if (!r) return;
    setGen(r);
    const n = latest(r);
    setActive(n);
    setSaved(null);
    setDrafts((d) => ({ ...d, [n]: itemsOf(r, n) }));
  };

  const save = async () => {
    setProblem(null);
    setSaving(true);
    const chosen = items.filter((i) => i.include);
    const r = await runAction(() => saveAiDrafts(gen.id, chosen.map((i) => toDraftInput(i.values))), {
      success: (q) => `${q.length} ${q.length === 1 ? "question" : "questions"} saved to the bank`,
      onProblem: (p) => {
        // 400s are keyed questions[i].field, i being the position among the chosen drafts.
        const byIndex = new Map<number, string>();
        for (const [k, msgs] of Object.entries(p.errors ?? {})) {
          const m = /^questions\[(\d+)\]/i.exec(k);
          if (m) byIndex.set(Number(m[1]), [byIndex.get(Number(m[1])), ...msgs].filter(Boolean).join(" "));
        }
        if (byIndex.size > 0) setDrafts((d) => ({ ...d, [active]: (d[active] ?? []).map((i) => ({ ...i, serverError: byIndex.get(chosen.indexOf(i)) })) }));
        else setProblem(p);
      },
    });
    setSaving(false);
    if (r) {
      setSaved(r.length);
      setDrafts((d) => ({ ...d, [active]: (d[active] ?? []).filter((i) => !i.include) }));
    }
  };

  return (
    <div className="space-y-4">
      <Card className="flex flex-wrap items-center gap-3 p-4">
        <div className="min-w-0 flex-1 text-sm">
          <p className="font-medium">
            {gen.subject} · {gen.classLevel} · {assessmentLabel[gen.assessmentType]}
          </p>
          <p className="text-muted">
            {gen.count} questions ({gen.easy} easy, {gen.medium} medium, {gen.hard} hard){gen.files.length > 0 ? ` · from ${gen.files.join(", ")}` : ""}
          </p>
        </div>
        <Button variant="secondary" onClick={() => void regenerate()} disabled={!gen.canRegenerate || blocker !== null || regenerating} loading={regenerating} loadingText="Writing…">
          <RefreshCw className="size-4" aria-hidden />
          Regenerate ({Math.max(0, left)} left)
        </Button>
      </Card>
      {blocker && gen.canRegenerate && <p className="text-sm text-muted">{blocker}</p>}
      {problem && (
        <Alert tone="danger" title="That didn't work">
          {problemMessage(problem)}
        </Alert>
      )}

      <nav aria-label="Versions" className="flex flex-wrap gap-1 rounded-input bg-surface-2 p-1 sm:inline-flex">
        {gen.versions.map((v) => (
          <button
            key={v.number}
            type="button"
            onClick={() => open(v.number)}
            aria-current={active === v.number ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center gap-1.5 rounded-[6px] px-3 text-sm", active === v.number ? "bg-surface font-medium shadow-low" : "text-muted hover:text-text")}
          >
            Version {v.number}
            {v.status === "Failed" && <CircleAlert className="size-3.5 text-danger" aria-label="failed" />}
          </button>
        ))}
      </nav>

      {regenerating ? (
        <Card className="flex flex-col items-center gap-3 p-10 text-center" aria-live="polite">
          <SchoolLoader size="md" />
          <p className="font-display text-lg font-semibold">Writing a new version…</p>
          <p className="text-sm text-muted">This usually takes 30 to 90 seconds. It avoids the questions in earlier versions.</p>
          <p className="tabular text-xs text-muted">{elapsed}s</p>
        </Card>
      ) : version?.status === "Failed" ? (
        <Alert tone="warning" title="This version couldn't be written">
          {version.message ?? "Maydel AI Assistant couldn't write questions from this request."} Nothing was taken from your allowance. Try regenerating, or change your guidance in a new request.
        </Alert>
      ) : (
        <>
          {saved !== null && (
            <Alert tone="success" title={`${saved} ${saved === 1 ? "question" : "questions"} saved to the bank`}>
              <Link href={bankHref(gen.subjectId, gen.classLevelId)} className="text-brand hover:underline">
                Open the {gen.subject} · {gen.classLevel} bank
              </Link>
              {items.length > 0 ? " · The rest are still here." : ""}
            </Alert>
          )}
          <ol className="space-y-3">
            {items.map((item, i) => {
              const clientProblem = draftProblem(item.values);
              const note = item.serverError ?? (item.include ? clientProblem : null) ?? (!item.edited ? item.aiProblem : null);
              return (
                <li key={item.key}>
                  <Card className={cn("space-y-3 p-4", !item.include && "opacity-70", note && item.include && "border-warning")}>
                    <div className="flex flex-wrap items-center gap-2">
                      <label className="inline-flex min-h-10 cursor-pointer items-center gap-2 text-sm font-medium">
                        <input type="checkbox" checked={item.include} onChange={(e) => update(item.key, { include: e.target.checked })} className="size-4 accent-[var(--brand)]" />
                        Include question {i + 1}
                      </label>
                      {item.own && <Badge>Your own</Badge>}
                      {item.own && (
                        <Button type="button" size="sm" variant="ghost" className="ml-auto" aria-label={`Remove question ${i + 1}`} onClick={() => setDrafts((d) => ({ ...d, [active]: (d[active] ?? []).filter((x) => x.key !== item.key) }))}>
                          <Trash2 className="size-4" aria-hidden />
                        </Button>
                      )}
                    </div>
                    {note && (
                      <p className="flex items-start gap-1.5 rounded-input bg-warning/10 px-3 py-2 text-sm text-warning" role="status">
                        <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                        <span>
                          <span className="font-medium">Fix before saving: </span>
                          {note}
                        </span>
                      </p>
                    )}
                    <DraftEditor id={item.key} values={item.values} topics={topics} onChange={(values) => update(item.key, { values, edited: true, serverError: undefined })} />
                  </Card>
                </li>
              );
            })}
          </ol>

          <div className="sticky bottom-0 z-10 -mx-4 flex flex-wrap items-center gap-2 border-t border-border bg-surface/95 px-4 py-3 backdrop-blur sm:mx-0 sm:rounded-card sm:border sm:shadow-low">
            <Button
              type="button"
              variant="secondary"
              onClick={() => setDrafts((d) => ({ ...d, [active]: [...(d[active] ?? []), { key: `i${++seq}`, include: true, values: draftValues(null), aiProblem: null, edited: false, own: true }] }))}
            >
              <Plus className="size-4" aria-hidden />
              Add my own question
            </Button>
            <span className="ml-auto text-sm text-muted">
              {invalid.length > 0 ? `${invalid.length} chosen ${invalid.length === 1 ? "question needs" : "questions need"} fixing` : `${selected.length} chosen`}
            </span>
            <Button type="button" onClick={() => void save()} loading={saving} loadingText="Saving…" disabled={selected.length === 0 || invalid.length > 0}>
              {saved !== null && items.length === 0 ? <CheckCircle2 className="size-4" aria-hidden /> : <Save className="size-4" aria-hidden />}
              Save selected to bank
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
