"use client";

import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { AlertTriangle, Check, CloudOff, History, Loader2, Lock, MessageSquare, RotateCcw } from "lucide-react";
import { ProblemCodes, problemMessage } from "@/lib/api/problem";
import type { GradeBand, GradeSheet, GradeSheetRow } from "@/lib/api/types";
import { saveGradeSheet } from "@/lib/assessment/actions";
import {
  COMMENT_MAX,
  backupKey,
  buildEntries,
  changedRows,
  draftsFromSheet,
  formatScore,
  gradeFor,
  mapServerErrors,
  nextCell,
  parseBackup,
  rowErrors,
  rowTotal,
  type Drafts,
  type RowDraft,
} from "@/lib/assessment/grid";
import { Badge, Button, cn } from "@/components/ui/primitives";
import { toast } from "@/components/ui/toast";

const AUTOSAVE_MS = 1500;

type Status =
  | { kind: "idle" }
  | { kind: "pending" }
  | { kind: "saving" }
  | { kind: "saved"; at: Date }
  | { kind: "invalid"; count: number }
  | { kind: "error"; message: string; retry: boolean }
  | { kind: "offline" };

function readStorage(key: string): string | null {
  try {
    return window.sessionStorage.getItem(key);
  } catch {
    return null;
  }
}
function writeStorage(key: string, value: string | null) {
  try {
    if (value === null) window.sessionStorage.removeItem(key);
    else window.sessionStorage.setItem(key, value);
  } catch {
    // private mode / storage full: the backup is best-effort
  }
}

const timeFormat = new Intl.DateTimeFormat("en-NG", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });

/**
 * The score grid (E5.1): one row per student, one column per component, then Total, Grade and an
 * optional comment. Keyboard-first: Tab/Shift+Tab along a row, Enter/Shift+Enter down/up, arrows at
 * the caret edges. Edits autosave ~1.5 s after the last keystroke, sending only the changed rows.
 */
export function ScoreGrid({ initial, bands }: { initial: GradeSheet; bands: GradeBand[] }) {
  const components = initial.components;
  const key = backupKey(initial.classRoomId, initial.subjectId, initial.termId);
  const cols = components.length + 1; // scores + comment

  const [rows, setRows] = useState<GradeSheetRow[]>(initial.rows);
  const [saved, setSaved] = useState<Drafts>(() => draftsFromSheet(initial));
  const [drafts, setDrafts] = useState<Drafts>(() => draftsFromSheet(initial));
  const [serverErrors, setServerErrors] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<Status>({ kind: "idle" });
  const [restored, setRestored] = useState(0);

  const draftsRef = useRef(drafts);
  const savedRef = useRef(saved);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const saving = useRef(false);
  const again = useRef(false);
  const cells = useRef(new Map<string, HTMLInputElement>());
  // Follow-up saves go through a ref so the save callback doesn't reference itself.
  const saveRef = useRef<() => Promise<void>>(async () => {});

  useEffect(() => {
    draftsRef.current = drafts;
  }, [drafts]);
  useEffect(() => {
    savedRef.current = saved;
  }, [saved]);

  const lockedIds = useMemo(() => new Set(rows.filter((r) => r.locked).map((r) => r.studentId)), [rows]);
  const dirtyIds = useMemo(() => changedRows(drafts, saved, components).filter((id) => !lockedIds.has(id)), [drafts, saved, components, lockedIds]);
  const dirty = dirtyIds.length > 0;

  /* ------------------------------------------------------------------ saving */

  const save = useCallback(async () => {
    clearTimeout(timer.current);
    if (saving.current) {
      again.current = true;
      return;
    }
    const snapshot = draftsRef.current;
    const ids = changedRows(snapshot, savedRef.current, components).filter((id) => !lockedIds.has(id));
    if (ids.length === 0) {
      setStatus((s) => (s.kind === "pending" ? { kind: "idle" } : s));
      return;
    }
    const entries = buildEntries(ids, snapshot, components);
    const invalidRows = ids.length - entries.length;
    if (entries.length === 0) {
      setStatus({ kind: "invalid", count: invalidRows });
      return;
    }

    saving.current = true;
    setStatus({ kind: "saving" });
    let result: Awaited<ReturnType<typeof saveGradeSheet>>;
    try {
      result = await saveGradeSheet({ classRoomId: initial.classRoomId, subjectId: initial.subjectId, termId: initial.termId, entries });
    } catch (error) {
      saving.current = false;
      if (error && typeof error === "object" && "digest" in error && String((error as { digest: unknown }).digest).startsWith("NEXT_REDIRECT")) throw error;
      setStatus({ kind: "offline" });
      return;
    }
    saving.current = false;

    if (result.ok) {
      const fresh = draftsFromSheet(result.data);
      setRows(result.data.rows);
      setSaved(fresh);
      savedRef.current = fresh;
      // Rows edited again while saving keep their newer text; the rest take the server's values.
      setDrafts((current) => {
        const next = { ...current };
        for (const e of entries) if (current[e.studentId] === snapshot[e.studentId]) next[e.studentId] = fresh[e.studentId];
        return next;
      });
      setServerErrors((errs) => Object.fromEntries(Object.entries(errs).filter(([k]) => !entries.some((e) => k.startsWith(`${e.studentId}:`)))));
      setStatus(invalidRows > 0 ? { kind: "invalid", count: invalidRows } : { kind: "saved", at: new Date() });
    } else {
      const p = result.problem;
      if (p.status === 400 && p.errors) {
        setServerErrors((errs) => ({ ...errs, ...mapServerErrors(p.errors, entries) }));
        setStatus({ kind: "error", message: "Some scores weren't accepted. Fix the highlighted cells.", retry: false });
      } else if (p.code === "assessment.report_published") {
        setStatus({ kind: "error", message: problemMessage(p), retry: false });
        toast.error("Some reports are published", { description: problemMessage(p) });
      } else if (p.status === 0 || p.status === 429 || p.status >= 500) {
        setStatus(p.status === 0 || p.status === 503 ? { kind: "offline" } : { kind: "error", message: problemMessage(p), retry: true });
      } else {
        setStatus({ kind: "error", message: problemMessage(p), retry: p.code !== ProblemCodes.passwordChangeRequired });
      }
    }

    if (again.current) {
      again.current = false;
      timer.current = setTimeout(() => void saveRef.current(), 300);
    }
  }, [components, initial.classRoomId, initial.subjectId, initial.termId, lockedIds]);

  useEffect(() => {
    saveRef.current = save;
  }, [save]);

  const schedule = useCallback(() => {
    clearTimeout(timer.current);
    setStatus({ kind: "pending" });
    timer.current = setTimeout(() => void save(), AUTOSAVE_MS);
  }, [save]);

  useEffect(() => () => clearTimeout(timer.current), []);

  // Retry when the connection comes back.
  useEffect(() => {
    const online = () => {
      if (changedRows(draftsRef.current, savedRef.current, components).length > 0) void save();
    };
    window.addEventListener("online", online);
    return () => window.removeEventListener("online", online);
  }, [components, save]);

  /* ------------------------------------------------------------------ backup */

  // Restore unsaved edits from this tab (e.g. after a dropped connection or a re-login). Once only.
  const restoreChecked = useRef(false);
  useEffect(() => {
    if (restoreChecked.current) return;
    restoreChecked.current = true;
    const backup = parseBackup(readStorage(key));
    if (!backup) return;
    const known = new Set(initial.rows.filter((r) => !r.locked).map((r) => r.studentId));
    const rowsToRestore = Object.entries(backup.rows).filter(([id]) => known.has(id));
    if (rowsToRestore.length === 0) return;
    const merged = { ...draftsRef.current };
    for (const [id, d] of rowsToRestore) merged[id] = d;
    const n = changedRows(merged, savedRef.current, components).length;
    if (n === 0) return;
    setDrafts(merged);
    setRestored(n);
    schedule();
  }, [key, initial.rows, components, schedule]);

  useEffect(() => {
    if (!dirty) {
      writeStorage(key, null);
      return;
    }
    const rowsOut: Drafts = {};
    for (const id of dirtyIds) rowsOut[id] = drafts[id];
    writeStorage(key, JSON.stringify({ at: Date.now(), rows: rowsOut }));
  }, [dirty, dirtyIds, drafts, key]);

  useEffect(() => {
    if (!dirty && status.kind !== "saving") return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty, status.kind]);

  /* ------------------------------------------------------------------ editing */

  const update = (studentId: string, change: (d: RowDraft) => RowDraft, cellKey: string) => {
    setDrafts((prev) => ({ ...prev, [studentId]: change(prev[studentId]) }));
    setServerErrors((errs) => {
      if (!errs[cellKey] && !errs[`${studentId}:row`]) return errs;
      const next = { ...errs };
      delete next[cellKey];
      delete next[`${studentId}:row`];
      return next;
    });
    schedule();
  };

  const focusCell = (row: number, col: number) => {
    const el = cells.current.get(`${row}:${col}`);
    if (el) {
      el.focus();
      el.select();
    }
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>, row: number, col: number) => {
    const el = e.currentTarget;
    const editable = (r: number) => !rows[r]?.locked;
    const go = (move: "left" | "right" | "up" | "down") => {
      const target = nextCell({ row, col }, move, { rows: rows.length, cols }, editable);
      if (target) {
        e.preventDefault();
        focusCell(target.row, target.col);
      }
    };
    if (e.key === "Enter") {
      e.preventDefault();
      go(e.shiftKey ? "up" : "down");
    } else if (e.key === "ArrowDown") go("down");
    else if (e.key === "ArrowUp") go("up");
    else if (e.key === "ArrowLeft" && el.selectionStart === 0 && el.selectionEnd === 0) go("left");
    else if (e.key === "ArrowRight" && el.selectionStart === el.value.length && el.selectionEnd === el.value.length) go("right");
    else if (e.key === "s" && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      void save();
    }
  };

  /* ------------------------------------------------------------------ summary */

  const complete = rows.filter((r) => {
    const d = drafts[r.studentId];
    return d && components.every((c) => (d.scores[c.id] ?? "").trim() !== "");
  }).length;
  const invalidCount = rows.reduce((n, r) => n + Object.keys(rowErrors(drafts[r.studentId], components)).filter((k) => k !== "comment").length, 0);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <SaveStatus status={status} dirty={dirty} onRetry={() => void save()} />
        <p className="tabular text-sm text-muted">
          {complete} of {rows.length} complete
          {invalidCount > 0 && <span className="ml-2 font-medium text-danger">· {invalidCount} invalid</span>}
        </p>
        <p className="ml-auto hidden text-xs text-muted lg:block">
          <kbd className="rounded border border-border px-1">Tab</kbd> next · <kbd className="rounded border border-border px-1">Enter</kbd> down ·{" "}
          <kbd className="rounded border border-border px-1">Ctrl S</kbd> save now
        </p>
      </div>

      {restored > 0 && (
        <div role="status" className="animate-rise flex items-center gap-2 rounded-input border border-brand/30 bg-brand/5 px-3 py-2 text-sm">
          <History className="size-4 text-brand" aria-hidden />
          Restored {restored} unsaved {restored === 1 ? "row" : "rows"} from this device. Saving now…
          <button type="button" className="ml-auto text-xs text-brand hover:underline" onClick={() => setRestored(0)}>
            Dismiss
          </button>
        </div>
      )}

      <div className="max-h-[calc(100dvh-14rem)] overflow-auto rounded-card border border-border bg-surface shadow-low">
        <table className="w-max min-w-full border-separate border-spacing-0 text-sm">
          <caption className="sr-only">
            Scores for {initial.subject}, {initial.classRoom}, {initial.term}
          </caption>
          <thead>
            <tr>
              <th scope="col" className="sticky top-0 left-0 z-30 min-w-36 border-r border-b border-border bg-surface-2 px-3 py-2 text-left text-xs font-semibold uppercase tracking-wide text-muted sm:min-w-56">
                Student
              </th>
              {components.map((c) => (
                <th key={c.id} scope="col" className="sticky top-0 z-20 border-b border-border bg-surface-2 px-2 py-2 text-center text-xs font-semibold text-muted">
                  <span className="block uppercase tracking-wide">{c.name}</span>
                  <span className="tabular block font-normal">/ {formatScore(c.maxScore)}</span>
                </th>
              ))}
              <th scope="col" className="sticky top-0 z-20 border-b border-l border-border bg-surface-2 px-3 py-2 text-center text-xs font-semibold uppercase tracking-wide text-muted">
                Total
              </th>
              <th scope="col" className="sticky top-0 z-20 border-b border-border bg-surface-2 px-3 py-2 text-center text-xs font-semibold uppercase tracking-wide text-muted">
                Grade
              </th>
              <th scope="col" className="sticky top-0 z-20 min-w-56 border-b border-l border-border bg-surface-2 px-3 py-2 text-left text-xs font-semibold uppercase tracking-wide text-muted">
                <span className="inline-flex items-center gap-1">
                  <MessageSquare className="size-3.5" aria-hidden />
                  Comment
                </span>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, r) => {
              const d = drafts[row.studentId];
              const errs = rowErrors(d, components);
              const isDirty = dirtyIds.includes(row.studentId);
              const total = isDirty ? rowTotal(d, components) : row.total;
              const band = isDirty ? gradeFor(total, bands) : row.grade ? { grade: row.grade, remark: row.remark ?? "" } : null;
              const rowError = serverErrors[`${row.studentId}:row`];
              return (
                <tr key={row.studentId} className={cn("group", row.locked && "bg-surface-2/40")}>
                  <th
                    scope="row"
                    className={cn(
                      "sticky left-0 z-10 border-r border-b border-border px-3 py-1.5 text-left font-normal",
                      row.locked ? "bg-surface-2" : "bg-surface group-focus-within:bg-brand/5",
                    )}
                  >
                    <span className="flex items-center gap-2">
                      {row.locked && (
                        <span title="Report published – ask an admin to unpublish" className="text-muted">
                          <Lock className="size-3.5" aria-label="Locked: report published" />
                        </span>
                      )}
                      <span className="min-w-0">
                        <span className="block truncate font-medium">{row.name}</span>
                        <span className="block font-mono text-[0.6875rem] text-muted">{row.admissionNumber}</span>
                      </span>
                    </span>
                    {rowError && (
                      <span role="alert" className="mt-0.5 block text-xs text-danger">
                        {rowError}
                      </span>
                    )}
                  </th>
                  {components.map((c, ci) => {
                    const cellKey = `${row.studentId}:${c.id}`;
                    const error = errs[c.id] ?? serverErrors[cellKey];
                    return (
                      <td key={c.id} className="border-b border-border px-1.5 py-1">
                        <input
                          ref={(el) => {
                            if (el) cells.current.set(`${r}:${ci}`, el);
                            else cells.current.delete(`${r}:${ci}`);
                          }}
                          value={d.scores[c.id] ?? ""}
                          onChange={(e) => update(row.studentId, (prev) => ({ ...prev, scores: { ...prev.scores, [c.id]: e.target.value } }), cellKey)}
                          onKeyDown={(e) => onKeyDown(e, r, ci)}
                          onFocus={(e) => e.currentTarget.select()}
                          readOnly={row.locked}
                          tabIndex={row.locked ? -1 : 0}
                          inputMode="decimal"
                          autoComplete="off"
                          aria-label={`${c.name} for ${row.name}, out of ${formatScore(c.maxScore)}`}
                          aria-invalid={error ? true : undefined}
                          title={error ?? (row.locked ? "Report published – ask an admin to unpublish" : undefined)}
                          className={cn(
                            "tabular block h-10 w-16 rounded-input border bg-surface px-2 text-center text-base outline-none transition-colors sm:text-sm",
                            "focus:border-brand focus:ring-2 focus:ring-brand/30",
                            error ? "border-danger bg-danger/8 text-danger" : "border-border bg-bg hover:border-muted/60",
                            row.locked && "cursor-not-allowed border-transparent bg-transparent text-muted",
                          )}
                        />
                        {error && <span className="sr-only">{error}</span>}
                      </td>
                    );
                  })}
                  <td className={cn("tabular border-b border-l border-border px-3 py-1.5 text-center font-semibold", isDirty && "text-brand")}>
                    {total === null ? <span className="text-muted">—</span> : formatScore(total)}
                  </td>
                  <td className="border-b border-border px-3 py-1.5 text-center" title={band?.remark}>
                    {band ? <Badge tone={band.grade.startsWith("A") ? "success" : band.grade.startsWith("F") ? "danger" : "neutral"}>{band.grade}</Badge> : <span className="text-muted">—</span>}
                  </td>
                  <td className="border-b border-l border-border px-1.5 py-1">
                    <input
                      ref={(el) => {
                        if (el) cells.current.set(`${r}:${components.length}`, el);
                        else cells.current.delete(`${r}:${components.length}`);
                      }}
                      value={d.comment}
                      maxLength={COMMENT_MAX + 50}
                      onChange={(e) => update(row.studentId, (prev) => ({ ...prev, comment: e.target.value }), `${row.studentId}:comment`)}
                      onKeyDown={(e) => onKeyDown(e, r, components.length)}
                      readOnly={row.locked}
                      tabIndex={row.locked ? -1 : 0}
                      autoComplete="off"
                      placeholder={row.locked ? "" : "Optional"}
                      aria-label={`Comment for ${row.name}`}
                      aria-invalid={errs.comment || serverErrors[`${row.studentId}:comment`] ? true : undefined}
                      title={errs.comment ?? serverErrors[`${row.studentId}:comment`] ?? d.comment}
                      className={cn(
                        "block h-10 w-full min-w-52 rounded-input border bg-surface px-2 text-base outline-none placeholder:text-muted/70 focus:border-brand focus:ring-2 focus:ring-brand/30 sm:text-sm",
                        errs.comment || serverErrors[`${row.studentId}:comment`] ? "border-danger" : "border-border bg-bg hover:border-muted/60",
                        row.locked && "cursor-not-allowed border-transparent bg-transparent text-muted",
                      )}
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function SaveStatus({ status, dirty, onRetry }: { status: Status; dirty: boolean; onRetry: () => void }) {
  let body: React.ReactNode;
  switch (status.kind) {
    case "pending":
      body = <span className="text-muted">Unsaved changes…</span>;
      break;
    case "saving":
      body = (
        <span className="inline-flex items-center gap-1.5 text-muted">
          <Loader2 className="size-4 animate-spin" aria-hidden />
          Saving…
        </span>
      );
      break;
    case "saved":
      body = (
        <span className="inline-flex items-center gap-1.5 text-success">
          <Check className="size-4" aria-hidden />
          Saved {timeFormat.format(status.at)}
        </span>
      );
      break;
    case "invalid":
      body = (
        <span className="inline-flex items-center gap-1.5 text-danger">
          <AlertTriangle className="size-4" aria-hidden />
          {status.count} {status.count === 1 ? "row isn't" : "rows aren't"} saved: fix the red cells
        </span>
      );
      break;
    case "offline":
      body = (
        <span className="inline-flex items-center gap-1.5 text-warning">
          <CloudOff className="size-4" aria-hidden />
          Not saved: you&apos;re offline. We&apos;ll retry when you reconnect.
          <Button variant="link" className="ml-1 text-sm" onClick={onRetry}>
            Retry now
          </Button>
        </span>
      );
      break;
    case "error":
      body = (
        <span className="inline-flex items-center gap-1.5 text-danger">
          <AlertTriangle className="size-4" aria-hidden />
          Not saved: {status.message}
          {status.retry && (
            <Button variant="link" className="ml-1 text-sm" onClick={onRetry}>
              <RotateCcw className="size-3.5" aria-hidden />
              Retry
            </Button>
          )}
        </span>
      );
      break;
    default:
      body = dirty ? <span className="text-muted">Unsaved changes</span> : <span className="text-muted">All changes saved</span>;
  }
  return (
    <p className="min-h-6 text-sm font-medium" role="status" aria-live="polite">
      {body}
    </p>
  );
}
