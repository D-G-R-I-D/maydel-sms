"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import { ChevronUp, Download, Paperclip, Pencil, Send, X } from "lucide-react";
import { problemMessage, toProblem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { editEntry, loadOlder, markBookRead } from "@/lib/book/actions";
import { MAX_BODY, entryTime, groupByDay, type BookEntry, type BookPage } from "@/lib/book/book";
import { HOMEWORK_ACCEPT, checkHomeworkFile, fileSize } from "@/lib/homework/homework";
import { Button, Card, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { toast } from "@/components/ui/toast";

const textareaClass =
  "block w-full resize-none rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/**
 * E17.2: the communication book as a chat. Your notes on the right; the other side on the left, each
 * labelled School or Family. Opening it clears your unread mark. Notes can be corrected for 15 minutes.
 */
export function BookThread({ page, viewer }: { page: BookPage; viewer: "School" | "Family" }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [older, setOlder] = useState<BookEntry[]>([]);
  const [hasOlder, setHasOlder] = useState(page.hasOlder);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const bottom = useRef<HTMLDivElement>(null);
  const entries = [...older, ...page.entries];

  useEffect(() => {
    void markBookRead(page.studentId).catch(() => undefined);
    bottom.current?.scrollIntoView({ block: "end" });
  }, [page.studentId, page.entries.length]);

  const showOlder = async () => {
    const first = entries[0];
    if (!first) return;
    setLoadingOlder(true);
    try {
      const r = await loadOlder(page.studentId, first.createdAt);
      if (r.ok) {
        setOlder((o) => [...r.data.entries, ...o]);
        setHasOlder(r.data.hasOlder);
      } else toast.error(problemMessage(r.problem));
    } catch {
      toast.error("Couldn't load older notes.");
    } finally {
      setLoadingOlder(false);
    }
  };

  return (
    <Card className="flex flex-col overflow-hidden p-0">
      <div className="max-h-[60dvh] min-h-64 space-y-4 overflow-y-auto p-4 sm:p-5" aria-live="polite">
        {hasOlder && (
          <div className="text-center">
            <Button size="sm" variant="ghost" loading={loadingOlder} loadingText="Loading…" onClick={() => void showOlder()}>
              <ChevronUp className="size-4" aria-hidden />
              Older notes
            </Button>
          </div>
        )}
        {entries.length === 0 ? (
          <p className="py-10 text-center text-sm text-muted">
            No notes yet. {viewer === "School" ? "Write the first note to the family below." : "Write to the class teacher below."}
          </p>
        ) : (
          groupByDay(entries).map((day) => (
            <section key={day.key} aria-label={day.heading} className="space-y-3">
              <p className="text-center text-xs font-medium text-muted">{day.heading}</p>
              {day.entries.map((e) => (
                <Note key={e.id} entry={e} onSaved={() => startTransition(() => router.refresh())} />
              ))}
            </section>
          ))
        )}
        <div ref={bottom} />
      </div>
      <Composer studentId={page.studentId} onSent={() => startTransition(() => router.refresh())} />
    </Card>
  );
}

function Note({ entry, onSaved }: { entry: BookEntry; onSaved: () => void }) {
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(entry.body);
  const [busy, setBusy] = useState(false);
  const mine = entry.isMine;
  return (
    <div className={cn("flex", mine ? "justify-end" : "justify-start")}>
      <div className={cn("max-w-[85%] rounded-card px-3.5 py-2.5 text-sm sm:max-w-[75%]", mine ? "rounded-br-sm bg-brand text-on-brand" : "rounded-bl-sm bg-surface-2 text-text")}>
        <p className={cn("mb-0.5 text-xs font-medium", mine ? "text-on-brand/80" : "text-muted")}>
          {entry.authorName} · {entry.side === "School" ? "School" : "Family"}
        </p>
        {editing ? (
          <div className="space-y-2">
            <label htmlFor={`edit-${entry.id}`} className="sr-only">
              Correct your note
            </label>
            <textarea id={`edit-${entry.id}`} value={text} onChange={(e) => setText(e.target.value)} rows={3} className={cn(textareaClass, "text-text")} />
            <div className="flex justify-end gap-2">
              <Button size="sm" variant="secondary" onClick={() => (setEditing(false), setText(entry.body))}>
                Cancel
              </Button>
              <Button
                size="sm"
                variant="secondary"
                loading={busy}
                loadingText="Saving…"
                disabled={!text.trim() || text.length > MAX_BODY}
                onClick={async () => {
                  setBusy(true);
                  const r = await runAction(() => editEntry(entry.studentId, entry.id, text.trim()), { success: "Note corrected" });
                  setBusy(false);
                  if (r) {
                    setEditing(false);
                    onSaved();
                  }
                }}
              >
                Save
              </Button>
            </div>
          </div>
        ) : (
          entry.body && <p className="whitespace-pre-line">{entry.body}</p>
        )}
        {entry.attachment && (
          <a
            href={`/api/media/books/${entry.studentId}/entries/${entry.id}/file`}
            className={cn("mt-1.5 inline-flex items-center gap-1.5 rounded-input px-2 py-1 text-xs underline-offset-2 hover:underline", mine ? "bg-white/15" : "bg-surface")}
          >
            <Download className="size-3.5" aria-hidden />
            {entry.attachment.fileName} ({fileSize(entry.attachment.size)})
          </a>
        )}
        <p className={cn("mt-1 flex items-center justify-end gap-2 text-[0.6875rem]", mine ? "text-on-brand/70" : "text-muted")}>
          {entry.editedAt && <span>edited</span>}
          <span>{entryTime(entry.createdAt)}</span>
          {entry.canEdit && !editing && (
            <button type="button" onClick={() => setEditing(true)} className="inline-flex items-center gap-0.5 underline-offset-2 hover:underline" aria-label="Correct this note">
              <Pencil className="size-3" aria-hidden />
              Edit
            </button>
          )}
        </p>
      </div>
    </div>
  );
}

function Composer({ studentId, onSent }: { studentId: string; onSent: () => void }) {
  const [body, setBody] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const picker = useRef<HTMLInputElement>(null);
  const tooLong = body.length > MAX_BODY;

  const send = async () => {
    setError(null);
    setBusy(true);
    try {
      const form = new FormData();
      if (body.trim()) form.append("body", body.trim());
      if (file) form.append("file", file);
      const res = await fetch(`/api/media/books/${studentId}/entries`, { method: "POST", body: form, credentials: "same-origin" });
      if (res.ok) {
        setBody("");
        setFile(null);
        if (picker.current) picker.current.value = "";
        onSent();
      } else {
        const json: unknown = await res.json().catch(() => null);
        setError(problemMessage(toProblem(res.status, json)));
      }
    } catch {
      setError("Couldn't send. Check your connection and try again.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-2 border-t border-border bg-surface p-3 sm:p-4">
      {error && <Alert tone="danger">{error}</Alert>}
      <label htmlFor={`book-${studentId}`} className="sr-only">
        Write a note
      </label>
      <textarea
        id={`book-${studentId}`}
        value={body}
        onChange={(e) => setBody(e.target.value)}
        rows={3}
        placeholder="Write a note…"
        className={textareaClass}
        aria-invalid={tooLong || undefined}
        onKeyDown={(e) => {
          if (e.key === "Enter" && (e.ctrlKey || e.metaKey) && (body.trim() || file) && !tooLong) void send();
        }}
      />
      <div className="flex flex-wrap items-center gap-2">
        <input
          ref={picker}
          type="file"
          accept={HOMEWORK_ACCEPT}
          className="sr-only"
          aria-label="Attach a file"
          onChange={(e) => {
            const f = e.target.files?.[0] ?? null;
            const bad = f && checkHomeworkFile(f);
            if (bad) {
              toast.error(bad);
              e.target.value = "";
            } else setFile(f);
          }}
        />
        {file ? (
          <span className="inline-flex max-w-full items-center gap-1.5 rounded-input bg-surface-2 px-2 py-1 text-xs">
            <Paperclip className="size-3.5" aria-hidden />
            <span className="truncate">{file.name}</span>
            <button type="button" onClick={() => (setFile(null), picker.current && (picker.current.value = ""))} aria-label={`Remove ${file.name}`} className="rounded p-0.5 hover:bg-surface">
              <X className="size-3" aria-hidden />
            </button>
          </span>
        ) : (
          <Button size="sm" variant="ghost" onClick={() => picker.current?.click()}>
            <Paperclip className="size-4" aria-hidden />
            Attach
          </Button>
        )}
        <span className={cn("ml-auto text-xs", tooLong ? "text-danger" : "text-muted")}>{body.length > MAX_BODY - 400 ? `${body.length}/${MAX_BODY}` : ""}</span>
        <Button loading={busy} loadingText="Sending…" disabled={(!body.trim() && !file) || tooLong} onClick={() => void send()}>
          <Send className="size-4" aria-hidden />
          Send
        </Button>
      </div>
    </div>
  );
}
