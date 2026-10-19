"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { Camera, FileUp, Minus, Plus, ShieldCheck, Sparkles, X } from "lucide-react";
import { isProblem, problemMessage, toProblem, type Problem } from "@/lib/api/problem";
import { AI_FILE_ACCEPT, ASSESSMENT_TYPES, MAX_AI_FILES, MAX_AI_QUESTIONS, MAX_GUIDANCE, aiRequestErrors, balancedMix, type AssessmentType } from "@/lib/cbt/ai";
import { KINDS, kindLabel, type AuthoringScope, type QuestionKind } from "@/lib/cbt/questions";
import { scopeKey, parseScope } from "@/lib/cbt/tests";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Card, Field, Input, Select, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { SchoolLoader } from "@/components/ui/school-loader";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

type Errors = Partial<Record<"kinds" | "count" | "easy" | "files" | "guidance" | "scope" | "assessmentType", string>>;

/**
 * E16.8: what to write. Scope, assessment type, kinds, how many with an easy/medium/hard mix, guidance and
 * up to 5 files (PDF or photos, camera on phones). Generating takes 30–90 s; the request goes through the BFF.
 */
export function AiRequestForm({ scopes, initialScope, blocker }: { scopes: AuthoringScope[]; initialScope?: string; blocker: string | null }) {
  const router = useRouter();
  const [scope, setScope] = useState(initialScope && scopes.some((s) => scopeKey(s.subjectId, s.classLevelId) === initialScope) ? initialScope : scopes[0] ? scopeKey(scopes[0].subjectId, scopes[0].classLevelId) : "");
  const [type, setType] = useState<AssessmentType>("Test");
  const [kinds, setKinds] = useState<QuestionKind[]>(["Objective"]);
  const [count, setCount] = useState("10");
  const [mix, setMix] = useState(balancedMix(10));
  const [guidance, setGuidance] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [errors, setErrors] = useState<Errors>({});
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const pick = useRef<HTMLInputElement>(null);
  const camera = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!busy) return;
    const started = Date.now();
    const id = setInterval(() => setElapsed(Math.round((Date.now() - started) / 1000)), 1000);
    return () => clearInterval(id);
  }, [busy]);

  const n = Number(count);
  const setCountAndMix = (v: string) => {
    setCount(v);
    if (/^\d+$/.test(v)) setMix(balancedMix(Math.min(MAX_AI_QUESTIONS, Number(v))));
  };
  const step = (key: keyof typeof mix, d: number) => setMix((m) => ({ ...m, [key]: Math.max(0, m[key] + d) }));
  const addFiles = (list: FileList | null) => {
    if (!list) return;
    // Copy now: the input is cleared right after, which empties the live FileList.
    const picked = Array.from(list);
    setFiles((cur) => [...cur, ...picked].slice(0, MAX_AI_FILES + 1));
    setErrors((e) => ({ ...e, files: undefined, guidance: undefined }));
  };

  const submit = async () => {
    setProblem(null);
    const e: Errors = aiRequestErrors({ count: n, ...mix, kinds, guidance, files });
    if (!scope) e.scope = "Choose the subject and class.";
    setErrors(e);
    if (Object.values(e).some(Boolean)) return;
    const form = new FormData();
    const { subjectId, classLevelId } = parseScope(scope);
    form.append("subjectId", subjectId);
    form.append("classLevelId", classLevelId);
    form.append("assessmentType", type);
    for (const k of kinds) form.append("kinds", k);
    form.append("count", String(n));
    form.append("easy", String(mix.easy));
    form.append("medium", String(mix.medium));
    form.append("hard", String(mix.hard));
    if (guidance.trim()) form.append("guidance", guidance.trim());
    for (const f of files) form.append("files", f);
    setBusy(true);
    try {
      const res = await fetch("/api/media/cbt/ai/generations", { method: "POST", body: form, credentials: "same-origin" });
      const json: unknown = await res.json().catch(() => null);
      if (!res.ok) {
        const p = isProblem(json) ? json : toProblem(res.status, json);
        if (p.status === 400 && p.errors) {
          const fe: Errors = {};
          for (const [k, msgs] of Object.entries(p.errors)) {
            const key = (k.charAt(0).toLowerCase() + k.slice(1)) as keyof Errors;
            fe[key === ("medium" as keyof Errors) || key === ("hard" as keyof Errors) ? "easy" : key] = msgs.join(" ");
          }
          setErrors(fe);
        } else setProblem(p);
        return;
      }
      const id = (json as { id?: string } | null)?.id;
      if (id) router.push(`/admin/cbt/ai/${id}`);
    } catch {
      setProblem(unreachableProblem());
    } finally {
      setBusy(false);
    }
  };

  if (busy) {
    return (
      <Card className="flex flex-col items-center gap-3 p-10 text-center" aria-live="polite">
        <SchoolLoader size="md" />
        <p className="font-display text-lg font-semibold">Writing {n} questions…</p>
        <p className="max-w-sm text-sm text-muted">This usually takes 30 to 90 seconds. Keep this page open; you&apos;ll see the drafts as soon as they&apos;re ready.</p>
        <p className="tabular text-xs text-muted">{elapsed}s</p>
      </Card>
    );
  }

  const total = mix.easy + mix.medium + mix.hard;
  return (
    <Card className="space-y-5 p-5">
      {blocker && (
        <Alert tone="warning" title="You can't write with AI right now">
          {blocker}
        </Alert>
      )}
      {problem && (
        <Alert tone="danger" title="Couldn't write the questions">
          {problemMessage(problem)}
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="ai-scope" label="Subject and class" error={errors.scope}>
          <Select id="ai-scope" value={scope} onChange={(e) => setScope(e.target.value)}>
            {scopes.map((s) => (
              <option key={scopeKey(s.subjectId, s.classLevelId)} value={scopeKey(s.subjectId, s.classLevelId)}>
                {s.subject} · {s.classLevel}
              </option>
            ))}
          </Select>
        </Field>
        <Field id="ai-type" label="For" error={errors.assessmentType}>
          <Select id="ai-type" value={type} onChange={(e) => setType(e.target.value as AssessmentType)}>
            {ASSESSMENT_TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      <fieldset aria-describedby={errors.kinds ? "ai-kinds-error" : undefined}>
        <legend className="mb-1.5 text-sm font-medium">Kinds of question</legend>
        <div className="flex flex-wrap gap-2">
          {KINDS.map((k) => {
            const on = kinds.includes(k);
            return (
              <label key={k} className={cn("inline-flex min-h-10 cursor-pointer items-center gap-2 rounded-full border px-3 text-sm", on ? "border-brand bg-brand/10 text-brand" : "border-border hover:bg-surface-2")}>
                <input type="checkbox" className="sr-only" checked={on} onChange={(e) => setKinds((cur) => (e.target.checked ? [...cur, k] : cur.filter((x) => x !== k)))} />
                {kindLabel[k]}
              </label>
            );
          })}
        </div>
        {errors.kinds && (
          <p id="ai-kinds-error" className="mt-1 text-sm text-danger" role="alert">
            {errors.kinds}
          </p>
        )}
      </fieldset>

      <div className="grid gap-4 sm:grid-cols-[10rem_1fr]">
        <Field id="ai-count" label="How many" error={errors.count}>
          <Input id="ai-count" value={count} onChange={(e) => setCountAndMix(e.target.value)} inputMode="numeric" className="tabular" aria-invalid={errors.count ? true : undefined} />
        </Field>
        <fieldset aria-describedby="ai-mix-hint">
          <legend className="mb-1.5 text-sm font-medium">Difficulty mix</legend>
          <div className="flex flex-wrap gap-3">
            {(["easy", "medium", "hard"] as const).map((k) => (
              <div key={k} className="flex items-center gap-1 rounded-input border border-border p-1">
                <Button type="button" size="sm" variant="ghost" aria-label={`Fewer ${k}`} onClick={() => step(k, -1)} disabled={mix[k] === 0}>
                  <Minus className="size-4" aria-hidden />
                </Button>
                <span className="tabular w-20 text-center text-sm" aria-live="polite">
                  {mix[k]} {k}
                </span>
                <Button type="button" size="sm" variant="ghost" aria-label={`More ${k}`} onClick={() => step(k, 1)}>
                  <Plus className="size-4" aria-hidden />
                </Button>
              </div>
            ))}
          </div>
          <p id="ai-mix-hint" className={cn("mt-1 text-xs", errors.easy || (Number.isInteger(n) && total !== n) ? "text-danger" : "text-muted")} role={errors.easy ? "alert" : undefined}>
            {errors.easy ?? (Number.isInteger(n) && total !== n ? `These add up to ${total}; make them add up to ${n}.` : `Adds up to ${total}.`)}
          </p>
        </fieldset>
      </div>

      <Field id="ai-guidance" label="What should the questions cover? (optional with files)" error={errors.guidance} hint="Topics, the week's lesson, curriculum notes, the style you want.">
        <textarea id="ai-guidance" value={guidance} onChange={(e) => setGuidance(e.target.value)} rows={4} maxLength={MAX_GUIDANCE} className={textareaClass} aria-invalid={errors.guidance ? true : undefined} />
      </Field>

      <div>
        <p className="mb-1.5 text-sm font-medium">Your material (optional with guidance)</p>
        <p className="mb-2 text-xs text-muted">Scheme of work or notes as PDFs (up to 5 MB) or photos of notes (up to 12 MB). Up to {MAX_AI_FILES} files.</p>
        {files.length > 0 && (
          <ul className="mb-2 space-y-1" aria-label="Files to send">
            {files.map((f, i) => (
              <li key={`${f.name}-${i}`} className="flex items-center gap-2 rounded-input border border-border px-3 py-1.5 text-sm">
                <span className="min-w-0 flex-1 truncate">{f.name}</span>
                <span className="tabular text-xs text-muted">{(f.size / (1024 * 1024)).toFixed(1)} MB</span>
                <button type="button" className="inline-flex size-8 items-center justify-center rounded-full text-muted hover:bg-surface-2" aria-label={`Remove ${f.name}`} onClick={() => setFiles((cur) => cur.filter((_, j) => j !== i))}>
                  <X className="size-4" aria-hidden />
                </button>
              </li>
            ))}
          </ul>
        )}
        <div className="flex flex-wrap gap-2">
          <input ref={pick} type="file" multiple accept={AI_FILE_ACCEPT} className="sr-only" tabIndex={-1} aria-hidden data-testid="ai-files" onChange={(e) => (addFiles(e.target.files), (e.target.value = ""))} />
          <input ref={camera} type="file" accept="image/*" capture="environment" className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => (addFiles(e.target.files), (e.target.value = ""))} />
          <Button type="button" size="sm" variant="secondary" onClick={() => pick.current?.click()} disabled={files.length >= MAX_AI_FILES}>
            <FileUp className="size-4" aria-hidden />
            Add files
          </Button>
          <Button type="button" size="sm" variant="secondary" className="sm:hidden" onClick={() => camera.current?.click()} disabled={files.length >= MAX_AI_FILES}>
            <Camera className="size-4" aria-hidden />
            Take a photo
          </Button>
        </div>
        {errors.files && (
          <p className="mt-1 text-sm text-danger" role="alert">
            {errors.files}
          </p>
        )}
      </div>

      <p className="flex items-start gap-2 rounded-input bg-surface-2 p-3 text-sm">
        <ShieldCheck className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
        Only your material, the subject and the class are sent to Maydel AI Assistant. No student information is ever sent.
      </p>

      <div className="flex justify-end">
        <Button onClick={() => void submit()} disabled={blocker !== null || scopes.length === 0}>
          <Sparkles className="size-4" aria-hidden />
          Write {Number.isInteger(n) && n > 0 ? n : ""} questions
        </Button>
      </div>
    </Card>
  );
}
