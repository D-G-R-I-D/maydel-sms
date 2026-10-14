import type { ClubLine, TraitRatingLine } from "@/lib/api/types";

/** Behaviour ratings & clubs (E2.5): client mirrors of the API rules in docs/handoffs/m1.2-behaviour.md. */

export const RATINGS = ["A", "B", "C", "D", "E"] as const;
export const CLUB_NAME_MAX = 40;
export const CLUB_RATING_MAX = 30;

export type ClubDraft = { key: string; name: string; rating: string };

let seq = 0;
export const newClub = (name = "", rating = ""): ClubDraft => ({ key: `club${++seq}`, name, rating });

export function clubsFromCard(clubs: readonly ClubLine[]): ClubDraft[] {
  return clubs.map((c) => newClub(c.name, c.rating ?? ""));
}

export function ratingsFromCard(lines: readonly TraitRatingLine[]): Record<string, string | null> {
  return Object.fromEntries(lines.map((l) => [l.traitId, l.rating]));
}

/** Club problems by row key (plus "_list" for list-wide ones). Blank rows are fine: the API drops them. */
export function clubErrors(clubs: readonly ClubDraft[], max: number): Record<string, string> {
  const errors: Record<string, string> = {};
  const filled = clubs.filter((c) => c.name.trim() !== "" || c.rating.trim() !== "");
  if (filled.length > max) errors._list = `At most ${max} clubs or activities.`;
  const seen = new Set<string>();
  for (const c of clubs) {
    const name = c.name.trim();
    if (!name && c.rating.trim()) errors[c.key] = "Add the club name.";
    else if (name.length > CLUB_NAME_MAX) errors[c.key] = `Keep the name under ${CLUB_NAME_MAX + 1} characters.`;
    else if (c.rating.trim().length > CLUB_RATING_MAX) errors[c.key] = `Keep the rating under ${CLUB_RATING_MAX + 1} characters.`;
    else if (name && seen.has(name.toLowerCase())) errors[c.key] = "This club is listed twice.";
    if (name) seen.add(name.toLowerCase());
  }
  return errors;
}

/** The PUT body: every trait (null = unrated) and the non-blank clubs. Replaces everything on the report. */
export function behaviourPayload(traitIds: readonly string[], ratings: Readonly<Record<string, string | null>>, clubs: readonly ClubDraft[]) {
  return {
    ratings: traitIds.map((traitId) => ({ traitId, rating: ratings[traitId] ?? null })),
    clubs: clubs.filter((c) => c.name.trim() !== "").map((c) => ({ name: c.name.trim(), rating: c.rating.trim() || null })),
  };
}

export function ratedCount(ratings: Readonly<Record<string, string | null>>): number {
  return Object.values(ratings).filter(Boolean).length;
}

/** Trait list rules for Settings → Behaviour traits: names required, ≤ 60 chars, unique (case-insensitive). */
export function traitIssues(rows: readonly { name: string }[]): string[] {
  const issues: string[] = [];
  const seen = new Set<string>();
  for (const r of rows) {
    const n = r.name.trim();
    if (!n) issues.push("Every trait needs a name.");
    else if (n.length > 60) issues.push(`"${n.slice(0, 20)}…" is too long (max 60).`);
    else if (seen.has(n.toLowerCase())) issues.push(`"${n}" appears twice.`);
    seen.add(n.toLowerCase());
  }
  return [...new Set(issues)];
}
