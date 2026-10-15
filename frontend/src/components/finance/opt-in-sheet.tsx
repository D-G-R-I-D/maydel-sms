"use client";

import { useEffect, useMemo, useState } from "react";
import { Search, Users } from "lucide-react";
import type { Loaded } from "@/lib/api/server";
import type { FeeItem, OptInStudent } from "@/lib/api/types";
import { loadOptIns, saveOptIns } from "@/lib/finance/actions";
import { runAction } from "@/lib/client/action";
import { Button, Input } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { CancelButton, DialogActions } from "@/components/ui/dialog";
import { LoaderStatus } from "@/components/ui/school-loader";

/**
 * "Who takes this?" for an optional fee item: students it could apply to, grouped by class, with
 * search and select-all per class. Only ticked students are billed for the item.
 */
export function OptInSheet({ item, close }: { item: FeeItem; close: () => void }) {
  const [state, setState] = useState<Loaded<OptInStudent[]> | null>(null);
  const [chosen, setChosen] = useState<Set<string>>(new Set());
  const [query, setQuery] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let live = true;
    loadOptIns(item.id)
      .then((r) => {
        if (!live) return;
        setState(r);
        if (r.ok) setChosen(new Set(r.data.filter((s) => s.optedIn).map((s) => s.studentId)));
      })
      .catch(() => live && setState({ ok: false, problem: { status: 0, title: "Couldn't reach the server. Reload the page and try again." } }));
    return () => {
      live = false;
    };
  }, [item.id]);

  const groups = useMemo(() => {
    if (!state?.ok) return [];
    const q = query.trim().toLowerCase();
    const map = new Map<string, OptInStudent[]>();
    for (const s of state.data) {
      if (q && !`${s.name} ${s.admissionNumber}`.toLowerCase().includes(q)) continue;
      const k = s.classRoom ?? "No class";
      map.set(k, [...(map.get(k) ?? []), s]);
    }
    return [...map.entries()].sort(([a], [b]) => a.localeCompare(b, undefined, { numeric: true }));
  }, [state, query]);

  if (!state) return <LoaderStatus label="Loading students…" />;
  if (!state.ok) return <ProblemState problem={state.problem} />;
  if (state.data.length === 0) {
    return (
      <EmptyState icon={Users} title="No students to choose from">
        {item.classLevel ? `There are no active students in ${item.classLevel}.` : "There are no active students yet."}
      </EmptyState>
    );
  }

  const toggle = (id: string, on: boolean) =>
    setChosen((prev) => {
      const next = new Set(prev);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });

  const save = async () => {
    setSaving(true);
    const saved = await runAction(() => saveOptIns(item.id, [...chosen]), { success: `${chosen.size} ${chosen.size === 1 ? "student takes" : "students take"} ${item.name}` });
    setSaving(false);
    if (saved) close();
  };

  return (
    <div className="space-y-4">
      <div className="relative">
        <label htmlFor="optin-search" className="sr-only">
          Search students
        </label>
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
        <Input id="optin-search" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search by name or admission number" className="pl-9" />
      </div>
      <p className="text-sm text-muted" aria-live="polite">
        <span className="tabular font-medium text-text">{chosen.size}</span> of {state.data.length} selected
      </p>
      {groups.length === 0 ? (
        <p className="py-6 text-center text-sm text-muted">No students match &ldquo;{query}&rdquo;.</p>
      ) : (
        <div className="space-y-4">
          {groups.map(([cls, students]) => {
            const all = students.every((s) => chosen.has(s.studentId));
            return (
              <fieldset key={cls} className="rounded-input border border-border">
                <legend className="sr-only">{cls}</legend>
                <label className="flex min-h-11 cursor-pointer items-center gap-3 border-b border-border bg-surface-2/60 px-3 text-sm font-medium">
                  <input
                    type="checkbox"
                    className="size-4 accent-[var(--brand)]"
                    checked={all}
                    onChange={(e) => {
                      const on = e.target.checked;
                      setChosen((prev) => {
                        const next = new Set(prev);
                        for (const s of students) {
                          if (on) next.add(s.studentId);
                          else next.delete(s.studentId);
                        }
                        return next;
                      });
                    }}
                    aria-label={`Select all in ${cls}`}
                  />
                  {cls}
                  <span className="ml-auto text-xs font-normal text-muted">{students.filter((s) => chosen.has(s.studentId)).length}/{students.length}</span>
                </label>
                <ul>
                  {students.map((s) => (
                    <li key={s.studentId}>
                      <label className="flex min-h-11 cursor-pointer items-center gap-3 px-3 text-sm hover:bg-surface-2/50">
                        <input type="checkbox" className="size-4 accent-[var(--brand)]" checked={chosen.has(s.studentId)} onChange={(e) => toggle(s.studentId, e.target.checked)} />
                        <span className="min-w-0 flex-1 truncate">{s.name}</span>
                        <span className="tabular text-xs text-muted">{s.admissionNumber}</span>
                      </label>
                    </li>
                  ))}
                </ul>
              </fieldset>
            );
          })}
        </div>
      )}
      <DialogActions>
        <CancelButton />
        <Button onClick={save} loading={saving} loadingText="Saving…">
          Save ({chosen.size})
        </Button>
      </DialogActions>
    </div>
  );
}
