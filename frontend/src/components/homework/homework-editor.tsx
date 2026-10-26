"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";
import { Download, Paperclip, Save, Trash2, Upload } from "lucide-react";
import { problemMessage, toProblem, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { deleteHomework, removeHomeworkFile, saveHomework } from "@/lib/homework/actions";
import {
  HOMEWORK_ACCEPT, MAX_FILES, checkHomeworkFile, fileSize, formToHomeworkInput, homeworkErrors, homeworkToForm,
  type ClassChoice, type Homework, type HomeworkForm,
} from "@/lib/homework/homework";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Button, Card, Field, Input, Select, buttonClasses, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";
import { RichEditor } from "@/components/website/rich-editor/rich-editor";

/**
 * E17.1: set or edit homework. Class and subject come from what this teacher teaches; the form
 * teacher (and admins) may also set general homework with no subject. Files attach once it's saved.
 */
export function HomeworkEditor({
  initial,
  choices,
  isAdmin,
  today,
  defaults,
}: {
  initial: Homework | null;
  choices: ClassChoice[];
  isAdmin: boolean;
  today: string;
  defaults: { classRoomId?: string; subjectId?: string; dueOn: string };
}) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [hw, setHw] = useState(initial);
  const [saved, setSaved] = useState<HomeworkForm>(() => homeworkToForm(initial, defaults));
  const [form, setForm] = useState<HomeworkForm>(saved);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const [uploading, setUploading] = useState<string | null>(null);
  const picker = useRef<HTMLInputElement>(null);

  const choice = choices.find((c) => c.classRoomId === form.classRoomId);
  const canGeneral = isAdmin || Boolean(choice?.isFormClass);
  const local = homeworkErrors(form, today, Boolean(hw));
  const server = problem?.errors ?? {};
  const errors = {
    classRoomId: local.classRoomId ?? server.classRoomId?.[0],
    subjectId: (!form.subjectId && !canGeneral && form.classRoomId ? "Choose the subject." : undefined) ?? server.subjectId?.[0],
    title: local.title ?? server.title?.[0],
    dueOn: local.dueOn ?? server.dueOn?.[0],
    instructions: server.instructionsHtml?.[0] ?? server.instructions?.[0],
  };
  const invalid = Object.keys(local).length > 0 || Boolean(!form.subjectId && !canGeneral);
  const dirty = JSON.stringify(form) !== JSON.stringify(saved);
  const set = <K extends keyof HomeworkForm>(k: K, v: HomeworkForm[K]) => setForm((f) => ({ ...f, [k]: v }));

  const save = async () => {
    setProblem(null);
    setBusy(true);
    const r = await runAction(() => saveHomework(hw?.id ?? null, formToHomeworkInput(form)), { success: hw ? "Homework saved" : "Homework set. The class has been notified.", onProblem: setProblem });
    setBusy(false);
    if (!r) return;
    const next = homeworkToForm(r, defaults);
    setHw(r);
    setSaved(next);
    setForm(next);
    if (!hw) startTransition(() => router.replace(`/admin/homework/${r.id}`));
  };

  const upload = async (files: FileList | null) => {
    if (!hw || !files?.length) return;
    const list = Array.from(files).slice(0, MAX_FILES - hw.files.length);
    for (const f of list) {
      const bad = checkHomeworkFile(f);
      if (bad) {
        toast.error(bad);
        continue;
      }
      setUploading(f.name);
      try {
        const body = new FormData();
        body.append("file", f);
        const res = await fetch(`/api/media/homework/${hw.id}/files`, { method: "POST", body, credentials: "same-origin" });
        const json: unknown = await res.json().catch(() => null);
        if (res.ok) setHw(json as Homework);
        else toast.error(`${f.name}: ${problemMessage(toProblem(res.status, json))}`);
      } catch {
        toast.error(`${f.name}: couldn't upload. Check your connection.`);
      }
    }
    setUploading(null);
    if (picker.current) picker.current.value = "";
    startTransition(() => router.refresh());
  };

  return (
    <div className="space-y-5">
      {problem && !problem.errors && (
        <Alert tone="danger" title="Couldn't save">
          {problemMessage(problem)}
        </Alert>
      )}
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
        <Card className="space-y-4 p-5">
          <Field id="hw-title" label="Title" error={errors.title}>
            <Input value={form.title} onChange={(e) => set("title", e.target.value)} placeholder="e.g. Fractions worksheet" {...describedBy("hw-title", errors.title)} />
          </Field>
          <div className="space-y-1.5">
            <span className="block text-sm font-medium">Instructions (optional)</span>
            <RichEditor id="hw-instructions" label="Instructions" initialHtml={form.instructionsHtml} onChange={(html) => set("instructionsHtml", html)} invalid={Boolean(errors.instructions)} />
            {errors.instructions && (
              <p className="text-sm text-danger" role="alert">
                {errors.instructions}
              </p>
            )}
          </div>
          <div className="space-y-2 border-t border-border pt-4">
            <p className="flex items-center gap-2 text-sm font-medium">
              <Paperclip className="size-4 text-brand" aria-hidden />
              Attachments <span className="font-normal text-muted">({hw?.files.length ?? 0}/{MAX_FILES})</span>
            </p>
            {!hw ? (
              <p className="text-sm text-muted">Save the homework first, then attach worksheets or pictures.</p>
            ) : (
              <>
                {hw.files.length > 0 && (
                  <ul className="divide-y divide-border rounded-input border border-border">
                    {hw.files.map((f) => (
                      <li key={f.id} className="flex items-center gap-2 px-3 py-2 text-sm">
                        <a href={`/api/media/homework/${hw.id}/files/${f.id}`} className="min-w-0 flex-1 truncate text-brand hover:underline">
                          {f.fileName}
                        </a>
                        <span className="text-xs text-muted">{fileSize(f.size)}</span>
                        <Button
                          size="sm"
                          variant="ghost"
                          aria-label={`Remove ${f.fileName}`}
                          onClick={async () => {
                            const r = await runAction(() => removeHomeworkFile(hw.id, f.id), { success: "Removed" });
                            if (r) setHw(r);
                          }}
                        >
                          <Trash2 className="size-4" aria-hidden />
                        </Button>
                      </li>
                    ))}
                  </ul>
                )}
                {hw.files.length < MAX_FILES && (
                  <>
                    <input ref={picker} type="file" multiple accept={HOMEWORK_ACCEPT} className="sr-only" aria-label="Choose files to attach" onChange={(e) => void upload(e.target.files)} />
                    <Button size="sm" variant="secondary" loading={uploading !== null} loadingText={`Uploading ${uploading ?? ""}…`} onClick={() => picker.current?.click()}>
                      <Upload className="size-4" aria-hidden />
                      Attach files
                    </Button>
                    <p className="text-xs text-muted">PDF, photos, Word, Excel or PowerPoint, up to 10 MB each.</p>
                  </>
                )}
              </>
            )}
          </div>
        </Card>

        <div className="space-y-5">
          <Card className="space-y-4 p-5">
            <Field id="hw-class" label="Class" error={errors.classRoomId}>
              <Select
                value={form.classRoomId}
                onChange={(e) => setForm((f) => ({ ...f, classRoomId: e.target.value, subjectId: "" }))}
                className="w-full"
                disabled={Boolean(hw)}
                {...describedBy("hw-class", errors.classRoomId)}
              >
                <option value="">Choose…</option>
                {choices.map((c) => (
                  <option key={c.classRoomId} value={c.classRoomId}>
                    {c.classRoom}
                  </option>
                ))}
              </Select>
            </Field>
            <Field id="hw-subject" label="Subject" error={errors.subjectId}>
              <Select value={form.subjectId} onChange={(e) => set("subjectId", e.target.value)} className="w-full" disabled={!choice} {...describedBy("hw-subject", errors.subjectId)}>
                {canGeneral ? <option value="">General (no subject)</option> : <option value="">Choose…</option>}
                {choice?.subjects.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field id="hw-due" label="Due" error={errors.dueOn}>
              <Input type="date" value={form.dueOn} min={hw ? undefined : today} onChange={(e) => set("dueOn", e.target.value)} {...describedBy("hw-due", errors.dueOn)} />
            </Field>
            <span className="flex items-center gap-3 text-sm">
              <Switch checked={form.acceptsSubmissions} onCheckedChange={(v) => set("acceptsSubmissions", v)} label="Students hand in online" />
              <span aria-hidden>Students hand in online</span>
            </span>
            <Button className="w-full" loading={busy} loadingText="Saving…" disabled={invalid || (!dirty && Boolean(hw))} onClick={() => void save()}>
              <Save className="size-4" aria-hidden />
              {hw ? "Save changes" : "Set homework"}
            </Button>
            {!hw && <p className="text-xs text-muted">Students and their parents get a notification when you set it.</p>}
          </Card>
          {hw && (
            <Card className="space-y-3 p-5">
              {hw.submitted != null && hw.classSize != null && (
                <a href={`/admin/homework/${hw.id}/hand-ins`} className={cn(buttonClasses("secondary"), "w-full")}>
                  <Download className="size-4" aria-hidden />
                  Hand-ins ({hw.submitted}/{hw.classSize})
                </a>
              )}
              <ConfirmDialog
                title="Delete this homework?"
                confirmLabel="Delete"
                pendingLabel="Deleting…"
                tone="destructive"
                trigger={
                  <button type="button" className={cn(buttonClasses("ghost", "sm"), "text-danger")}>
                    <Trash2 className="size-4" aria-hidden />
                    Delete homework
                  </button>
                }
                onConfirm={async () => {
                  const r = await runAction(() => deleteHomework(hw.id), { success: "Homework deleted" });
                  if (r) startTransition(() => router.replace("/admin/homework"));
                  return Boolean(r);
                }}
              >
                &ldquo;{hw.title}&rdquo; will be removed for the class. Homework that has hand-ins can&apos;t be deleted.
              </ConfirmDialog>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
