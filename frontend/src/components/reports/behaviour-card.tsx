"use client";

import { useState } from "react";
import { Lock, Plus, Save, Trash2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { ClubLine, RatingKeyEntry, TraitRatingLine } from "@/lib/api/types";
import { saveBehaviour } from "@/lib/reports/actions";
import { CLUB_NAME_MAX, CLUB_RATING_MAX, RATINGS, behaviourPayload, clubErrors, clubsFromCard, newClub, ratedCount, ratingsFromCard, type ClubDraft } from "@/lib/reports/behaviour";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

/**
 * Behaviour (A–E per trait) and clubs on a report (E2.5). Editable by the form teacher or an admin
 * while Draft; read-only otherwise. Saves everything in one PUT. Never affects scores or position.
 */
export function BehaviourCard({
  studentId,
  termId,
  behaviour,
  clubs,
  ratingKey,
  maxClubs,
  canEdit,
  published,
}: {
  studentId: string;
  termId: string;
  behaviour: TraitRatingLine[];
  clubs: ClubLine[];
  ratingKey: RatingKeyEntry[];
  maxClubs: number;
  canEdit: boolean;
  published: boolean;
}) {
  const editable = canEdit && !published;
  const [traits, setTraits] = useState(behaviour);
  const [savedRatings, setSavedRatings] = useState(() => ratingsFromCard(behaviour));
  const [ratings, setRatings] = useState(() => ratingsFromCard(behaviour));
  const [savedClubs, setSavedClubs] = useState(() => clubsFromCard(clubs));
  const [clubRows, setClubRows] = useState<ClubDraft[]>(() => clubsFromCard(clubs));
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);

  const errors = clubErrors(clubRows, maxClubs);
  const payload = behaviourPayload(traits.map((t) => t.traitId), ratings, clubRows);
  const savedPayload = behaviourPayload(traits.map((t) => t.traitId), savedRatings, savedClubs);
  const dirty = JSON.stringify(payload) !== JSON.stringify(savedPayload);
  const rated = ratedCount(ratings);

  const save = async () => {
    setProblem(null);
    if (Object.keys(errors).length > 0) return;
    setBusy(true);
    const card = await runAction(() => saveBehaviour(studentId, termId, payload), { success: "Behaviour & clubs saved", onProblem: setProblem });
    setBusy(false);
    if (card) {
      setTraits(card.behaviour);
      const r = ratingsFromCard(card.behaviour);
      setSavedRatings(r);
      setRatings(r);
      const c = clubsFromCard(card.clubs);
      setSavedClubs(c);
      setClubRows(c.length ? c : []);
    }
  };

  const meaning = (r: string) => ratingKey.find((k) => k.rating === r)?.meaning ?? r;

  return (
    <Card className="mt-5 overflow-hidden">
      <div className="flex flex-wrap items-start gap-3 border-b border-border px-5 py-4">
        <div className="min-w-0 flex-1">
          <h2 className="flex flex-wrap items-center gap-2 font-display text-lg font-semibold">
            Behaviour &amp; clubs
            <Badge tone={rated === traits.length && traits.length > 0 ? "success" : "neutral"}>
              {rated}/{traits.length} rated
            </Badge>
            {published && (
              <Badge>
                <Lock className="size-3" aria-hidden />
                Published
              </Badge>
            )}
          </h2>
          <p className="text-sm text-muted">Doesn&apos;t affect scores or position.{!canEdit && " Only the form teacher or an admin can change these."}</p>
        </div>
        <ul className="flex flex-wrap gap-1.5 text-xs" aria-label="Rating key">
          {ratingKey.map((k) => (
            <li key={k.rating} className="rounded-full bg-surface-2 px-2.5 py-0.5">
              <span className="font-semibold">{k.rating}</span> {k.meaning}
            </li>
          ))}
        </ul>
      </div>

      <div className="space-y-6 p-5">
        {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
          <Alert tone="danger" title="Couldn't save">
            {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
          </Alert>
        )}

        <ul className="grid gap-x-8 gap-y-2 lg:grid-cols-2">
          {traits.map((t) => {
            const value = ratings[t.traitId] ?? null;
            return (
              <li key={t.traitId} className="flex flex-wrap items-center justify-between gap-2 py-1">
                <span className="min-w-0 flex-1 text-sm">{t.trait}</span>
                {editable ? (
                  <fieldset className="inline-flex rounded-input border border-border bg-surface-2/60 p-0.5">
                    <legend className="sr-only">{t.trait}</legend>
                    {RATINGS.map((r) => (
                      <label
                        key={r}
                        title={meaning(r)}
                        className={cn(
                          "relative inline-flex size-9 cursor-pointer items-center justify-center rounded-[6px] text-sm font-semibold transition-colors",
                          "has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-brand",
                          value === r ? "bg-brand text-on-brand shadow-sm" : "text-muted hover:bg-surface hover:text-text",
                        )}
                      >
                        <input
                          type="radio"
                          name={`trait-${t.traitId}`}
                          value={r}
                          checked={value === r}
                          onChange={() => setRatings((prev) => ({ ...prev, [t.traitId]: r }))}
                          className="sr-only"
                        />
                        {r}
                        <span className="sr-only"> – {meaning(r)}</span>
                      </label>
                    ))}
                    <label
                      title="Not rated"
                      className={cn(
                        "relative inline-flex size-9 cursor-pointer items-center justify-center rounded-[6px] text-sm transition-colors",
                        "has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-brand",
                        value === null ? "bg-surface text-text shadow-sm" : "text-muted hover:bg-surface",
                      )}
                    >
                      <input
                        type="radio"
                        name={`trait-${t.traitId}`}
                        value=""
                        checked={value === null}
                        onChange={() => setRatings((prev) => ({ ...prev, [t.traitId]: null }))}
                        className="sr-only"
                      />
                      –<span className="sr-only">Not rated</span>
                    </label>
                  </fieldset>
                ) : (
                  <span className={cn("inline-flex size-8 items-center justify-center rounded-input text-sm font-semibold", value ? "bg-brand/10 text-brand" : "text-muted")} title={value ? meaning(value) : "Not rated"}>
                    {value ?? "–"}
                  </span>
                )}
              </li>
            );
          })}
        </ul>

        <section aria-labelledby="clubs-heading">
          <h3 id="clubs-heading" className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted">
            Clubs &amp; activities <span className="font-normal normal-case tracking-normal">(up to {maxClubs})</span>
          </h3>
          {clubRows.length === 0 && <p className="mb-2 text-sm text-muted">None yet.</p>}
          <ul className="space-y-2">
            {clubRows.map((c, i) =>
              editable ? (
                <li key={c.key}>
                  <div className="flex flex-wrap items-center gap-2 sm:flex-nowrap">
                    <div className="min-w-[10rem] flex-1">
                      <Input
                        value={c.name}
                        onChange={(e) => setClubRows((rows) => rows.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))}
                        aria-label={`Club ${i + 1} name`}
                        placeholder="e.g. Coding club"
                        maxLength={CLUB_NAME_MAX + 10}
                        aria-invalid={errors[c.key] ? true : undefined}
                        className="h-10 min-h-0"
                      />
                    </div>
                    <Input
                      value={c.rating}
                      onChange={(e) => setClubRows((rows) => rows.map((x, j) => (j === i ? { ...x, rating: e.target.value } : x)))}
                      aria-label={`Club ${i + 1} rating`}
                      placeholder="Rating (optional)"
                      maxLength={CLUB_RATING_MAX + 10}
                      className="h-10 min-h-0 w-full sm:w-44"
                    />
                    <Button variant="ghost" size="sm" className="size-10 px-0" onClick={() => setClubRows((rows) => rows.filter((_, j) => j !== i))} aria-label={`Remove club ${c.name || i + 1}`}>
                      <Trash2 className="size-4 text-danger" aria-hidden />
                    </Button>
                  </div>
                  {errors[c.key] && (
                    <p role="alert" className="mt-1 text-sm text-danger">
                      {errors[c.key]}
                    </p>
                  )}
                </li>
              ) : (
                <li key={c.key} className="text-sm">
                  <span className="font-medium">{c.name}</span>
                  {c.rating && <span className="text-muted"> — {c.rating}</span>}
                </li>
              ),
            )}
          </ul>
          {errors._list && (
            <p role="alert" className="mt-1 text-sm text-danger">
              {errors._list}
            </p>
          )}
          {editable && (
            <Button variant="secondary" size="sm" className="mt-3" disabled={clubRows.length >= maxClubs} onClick={() => setClubRows((rows) => [...rows, newClub()])}>
              <Plus className="size-4" aria-hidden />
              Add club or activity
            </Button>
          )}
        </section>

        {editable && (
          <div className="flex items-center justify-end gap-3 border-t border-border pt-4">
            <span className="text-sm text-muted" aria-live="polite">
              {dirty ? "Unsaved changes" : "All saved"}
            </span>
            <Button onClick={save} loading={busy} loadingText="Saving…" disabled={!dirty || Object.keys(errors).length > 0}>
              <Save className="size-4" aria-hidden />
              Save behaviour &amp; clubs
            </Button>
          </div>
        )}
      </div>
    </Card>
  );
}
