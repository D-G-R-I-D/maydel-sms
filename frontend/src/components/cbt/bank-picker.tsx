"use client";

import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, Plus, Search } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { searchBank } from "@/lib/cbt/actions";
import { difficultyTone, firstLine, kindLabel, type Question, type QuestionKind } from "@/lib/cbt/questions";
import type { QuestionBrief } from "@/lib/cbt/tests";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Badge, Button, Input, Select } from "@/components/ui/primitives";
import { DialogActions, CancelButton, FormDialog } from "@/components/ui/dialog";
import { SchoolLoader } from "@/components/ui/school-loader";

const PAGE_SIZE = 20;
export const toBrief = (q: Question): QuestionBrief => ({ id: q.id, kind: q.kind, text: q.text.slice(0, 200), topic: q.topic, difficulty: q.difficulty, hasImage: q.hasImage, isArchived: false });

type Query = { search: string; topic: string; page: number };

/**
 * E16.2: pick questions of one kind from the test's bank. Questions already in the test show where
 * they're used and can't be picked again. Picked questions are added to the end of the section.
 */
export function BankPicker({
  subjectId,
  classLevelId,
  kind,
  topics,
  used,
  onAdd,
}: {
  subjectId: string;
  classLevelId: string;
  kind: QuestionKind;
  topics: string[];
  /** question id → the section title it's already in */
  used: Map<string, string>;
  onAdd: (picked: QuestionBrief[]) => void;
}) {
  return (
    <FormDialog
      title={`Add ${kindLabel[kind].toLowerCase()} questions`}
      description="From the bank for this subject and class. They're added in the order you tick them."
      size="lg"
      trigger={
        <Button type="button" size="sm" variant="secondary">
          <Plus className="size-4" aria-hidden />
          Add questions
        </Button>
      }
    >
      {(close) => (
        <PickerBody
          subjectId={subjectId}
          classLevelId={classLevelId}
          kind={kind}
          topics={topics}
          used={used}
          onAdd={(picked) => {
            onAdd(picked);
            close();
          }}
        />
      )}
    </FormDialog>
  );
}

function PickerBody({ subjectId, classLevelId, kind, topics, used, onAdd }: Omit<Parameters<typeof BankPicker>[0], "onAdd"> & { onAdd: (picked: QuestionBrief[]) => void }) {
  const [query, setQuery] = useState<Query>({ search: "", topic: "", page: 1 });
  const [text, setText] = useState("");
  const [result, setResult] = useState<{ items: Question[]; totalCount: number } | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [loading, setLoading] = useState(true);
  const [picked, setPicked] = useState<QuestionBrief[]>([]);

  useEffect(() => {
    let live = true;
    searchBank({ subjectId, classLevelId, kind, search: query.search, topic: query.topic, page: query.page })
      .then((r) => {
        if (!live) return;
        if (r.ok) {
          setResult({ items: r.data.items, totalCount: r.data.totalCount });
          setProblem(null);
        } else setProblem(r.problem);
      })
      .catch(() => live && setProblem(unreachableProblem()))
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
  }, [subjectId, classLevelId, kind, query]);

  const go = (q: Partial<Query>) => {
    setLoading(true);
    setQuery((cur) => ({ ...cur, page: 1, ...q }));
  };
  const pages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;
  const toggle = (q: Question, on: boolean) => setPicked((cur) => (on ? [...cur, toBrief(q)] : cur.filter((p) => p.id !== q.id)));

  return (
    <div className="space-y-3">
      <div className="grid gap-2 sm:grid-cols-[1fr_12rem]">
        <form
          role="search"
          className="relative"
          onSubmit={(e) => {
            e.preventDefault();
            go({ search: text.trim() });
          }}
        >
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
          <Input value={text} onChange={(e) => setText(e.target.value)} onBlur={() => text.trim() !== query.search && go({ search: text.trim() })} placeholder="Search, then Enter" aria-label="Search the bank" className="pl-9" />
        </form>
        <Select aria-label="Topic" value={query.topic} onChange={(e) => go({ topic: e.target.value })} disabled={topics.length === 0}>
          <option value="">All topics</option>
          {topics.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </Select>
      </div>

      <div className="min-h-48 rounded-input border border-border" aria-busy={loading}>
        {problem ? (
          <p className="p-4 text-sm text-danger" role="alert">
            {problemMessage(problem)}
          </p>
        ) : !result ? (
          <div className="flex justify-center p-8">
            <SchoolLoader size="sm" />
          </div>
        ) : result.items.length === 0 ? (
          <p className="p-6 text-center text-sm text-muted">{query.search || query.topic ? "No questions match." : `No ${kindLabel[kind].toLowerCase()} questions in this bank yet.`}</p>
        ) : (
          <ul className="max-h-[50vh] divide-y divide-border overflow-y-auto">
            {result.items.map((q) => {
              const where = used.get(q.id);
              const on = picked.some((p) => p.id === q.id);
              return (
                <li key={q.id}>
                  <label className={`flex gap-3 px-3 py-2.5 ${where ? "cursor-not-allowed opacity-60" : "cursor-pointer hover:bg-surface-2"}`}>
                    <input type="checkbox" className="mt-1 size-4 shrink-0 accent-[var(--brand)]" checked={on || Boolean(where)} disabled={Boolean(where)} onChange={(e) => toggle(q, e.target.checked)} />
                    <span className="flex min-w-0 flex-1 flex-col text-sm break-words">
                      {firstLine(q.text, 160)}
                      <span className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-muted">
                        <Badge tone={difficultyTone[q.difficulty]}>{q.difficulty}</Badge>
                        {q.topic && <span>{q.topic}</span>}
                        {where && <span>· Already in {where}</span>}
                      </span>
                    </span>
                  </label>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {result && result.totalCount > PAGE_SIZE && (
        <div className="flex items-center justify-between text-sm">
          <span className="tabular text-muted">
            Page {query.page} of {pages}
          </span>
          <span className="flex gap-2">
            <Button type="button" size="sm" variant="secondary" aria-label="Previous page" disabled={query.page <= 1 || loading} onClick={() => go({ page: query.page - 1 })}>
              <ChevronLeft className="size-4" aria-hidden />
            </Button>
            <Button type="button" size="sm" variant="secondary" aria-label="Next page" disabled={query.page >= pages || loading} onClick={() => go({ page: query.page + 1 })}>
              <ChevronRight className="size-4" aria-hidden />
            </Button>
          </span>
        </div>
      )}

      <DialogActions>
        <CancelButton />
        <Button type="button" disabled={picked.length === 0} onClick={() => onAdd(picked)}>
          {picked.length === 0 ? "Add questions" : `Add ${picked.length} ${picked.length === 1 ? "question" : "questions"}`}
        </Button>
      </DialogActions>
    </div>
  );
}
