"use client";

import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";
import { Paperclip, Send, X } from "lucide-react";
import { problemMessage, toProblem } from "@/lib/api/problem";
import { HOMEWORK_ACCEPT, MAX_FILES, checkHomeworkFile, fileSize } from "@/lib/homework/homework";
import { Button } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { toast } from "@/components/ui/toast";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/** E17.1 student: hand in text and/or up to 5 files, with upload progress. Handing in again replaces it. */
export function HandInForm({ studentId, homeworkId, again }: { studentId: string; homeworkId: string; again: boolean }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [text, setText] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const picker = useRef<HTMLInputElement>(null);
  const empty = !text.trim() && files.length === 0;

  const add = (list: FileList | null) => {
    const next = [...files];
    for (const f of Array.from(list ?? [])) {
      const bad = checkHomeworkFile(f);
      if (bad) toast.error(bad);
      else if (next.length < MAX_FILES) next.push(f);
    }
    setFiles(next);
    if (picker.current) picker.current.value = "";
  };

  const submit = () => {
    setError(null);
    const body = new FormData();
    if (text.trim()) body.append("text", text.trim());
    for (const f of files) body.append("files", f);
    // XMLHttpRequest for upload progress (fetch has none).
    const xhr = new XMLHttpRequest();
    xhr.open("POST", `/api/media/portal/children/${studentId}/homework/${homeworkId}/hand-in`);
    xhr.withCredentials = true;
    xhr.upload.onprogress = (e) => e.lengthComputable && setProgress(Math.round((e.loaded / e.total) * 100));
    xhr.onload = () => {
      setProgress(null);
      let json: unknown = null;
      try {
        json = JSON.parse(xhr.responseText);
      } catch {
        // Not JSON (e.g. a proxy error page); the status alone is described below.
      }
      if (xhr.status >= 200 && xhr.status < 300) {
        toast.success("Handed in");
        setText("");
        setFiles([]);
        startTransition(() => router.refresh());
      } else setError(problemMessage(toProblem(xhr.status, json)));
    };
    xhr.onerror = () => {
      setProgress(null);
      setError("Couldn't upload. Check your connection and try again.");
    };
    setProgress(0);
    xhr.send(body);
  };

  return (
    <div className="space-y-3">
      {error && <Alert tone="danger">{error}</Alert>}
      <label htmlFor={`hand-in-${homeworkId}`} className="block text-sm font-medium">
        {again ? "Hand in again" : "Your answer"}
      </label>
      <textarea id={`hand-in-${homeworkId}`} value={text} onChange={(e) => setText(e.target.value)} rows={6} className={textareaClass} placeholder="Type your answer, or attach your work below." />
      {files.length > 0 && (
        <ul className="space-y-1 text-sm">
          {files.map((f, i) => (
            <li key={`${f.name}-${i}`} className="flex items-center gap-2">
              <Paperclip className="size-3.5 text-muted" aria-hidden />
              <span className="min-w-0 flex-1 truncate">{f.name}</span>
              <span className="text-xs text-muted">{fileSize(f.size)}</span>
              <button type="button" onClick={() => setFiles(files.filter((_, j) => j !== i))} className="rounded p-1 text-muted hover:text-text" aria-label={`Remove ${f.name}`}>
                <X className="size-3.5" aria-hidden />
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <input ref={picker} type="file" multiple accept={HOMEWORK_ACCEPT} className="sr-only" aria-label="Choose files" onChange={(e) => add(e.target.files)} />
        <Button variant="secondary" size="sm" disabled={files.length >= MAX_FILES || progress !== null} onClick={() => picker.current?.click()}>
          <Paperclip className="size-4" aria-hidden />
          Attach files ({files.length}/{MAX_FILES})
        </Button>
        <Button className="ml-auto" loading={progress !== null} loadingText={progress !== null && progress < 100 ? `Uploading ${progress}%` : "Handing in…"} disabled={empty} onClick={submit}>
          <Send className="size-4" aria-hidden />
          Hand in
        </Button>
      </div>
      <p className="text-xs text-muted">Photos of your work, PDFs, or Word/Excel/PowerPoint files, up to 10 MB each.{again ? " This replaces what you handed in before." : ""}</p>
    </div>
  );
}
