import type { AssessmentSettings, ComponentInput, GradeBand } from "@/lib/api/types";

/** Editable copies of the assessment settings (E2.4); numbers are kept as typed text. */
export type ComponentRow = { key: string; id: string | null; name: string; maxScore: string; isActive: boolean; hasScores: boolean };
export type BandRow = { key: string; grade: string; minScore: string; maxScore: string; remark: string };

let seq = 0;
const newKey = () => `k${++seq}`;

export function toRows(s: AssessmentSettings): { components: ComponentRow[]; bands: BandRow[] } {
  return {
    components: [...s.components]
      .sort((a, b) => a.order - b.order)
      .map((c) => ({ key: newKey(), id: c.id, name: c.name, maxScore: String(c.maxScore), isActive: c.isActive, hasScores: c.hasScores })),
    bands: [...s.bands]
      .sort((a, b) => b.minScore - a.minScore)
      .map((b) => ({ key: newKey(), grade: b.grade, minScore: String(b.minScore), maxScore: String(b.maxScore), remark: b.remark })),
  };
}

export function blankComponent(): ComponentRow {
  return { key: newKey(), id: null, name: "", maxScore: "", isActive: true, hasScores: false };
}

export function blankBand(): BandRow {
  return { key: newKey(), grade: "", minScore: "", maxScore: "", remark: "" };
}

const num = (v: string) => (/^\d+(\.\d{1,2})?$/.test(v.trim()) ? Number(v) : NaN);

/** Sum of active components' max scores (what must equal 100). NaN-safe: invalid cells count as 0. */
export function activeTotal(components: readonly ComponentRow[]): number {
  const t = components.filter((c) => c.isActive).reduce((sum, c) => sum + (Number.isNaN(num(c.maxScore)) ? 0 : num(c.maxScore)), 0);
  return Math.round(t * 100) / 100;
}

/** Problems that block Save, written for admins. Empty = OK to save. */
export function settingsIssues(components: readonly ComponentRow[], bands: readonly BandRow[]): string[] {
  const issues: string[] = [];
  const active = components.filter((c) => c.isActive);
  if (active.length === 0) issues.push("At least one component must be active.");
  const names = new Set<string>();
  for (const c of components) {
    const n = c.name.trim();
    if (!n) issues.push("Every component needs a name.");
    else if (n.length > 50) issues.push(`"${n.slice(0, 20)}…" is too long (max 50).`);
    else if (names.has(n.toLowerCase())) issues.push(`Two components are called "${n}".`);
    names.add(n.toLowerCase());
    const m = num(c.maxScore);
    if (Number.isNaN(m) || m <= 0 || m > 100) issues.push(`${n || "A component"}: max must be a number from 1 to 100.`);
  }
  const total = activeTotal(components);
  if (total !== 100) issues.push(`Active components add up to ${total}; they must total 100.`);

  if (bands.length === 0) issues.push("Add at least one grade band.");
  const grades = new Set<string>();
  for (const b of bands) {
    const g = b.grade.trim();
    const min = num(b.minScore);
    const max = num(b.maxScore);
    if (!g) issues.push("Every band needs a grade.");
    else if (grades.has(g.toUpperCase())) issues.push(`Grade ${g} appears twice.`);
    grades.add(g.toUpperCase());
    if (Number.isNaN(min) || Number.isNaN(max) || min > max || max > 100) issues.push(`${g || "A band"}: min and max must be 0–100 with min ≤ max.`);
    if (!b.remark.trim()) issues.push(`${g || "A band"}: add a remark (e.g. "Excellent").`);
  }
  const mins = bands.map((b) => num(b.minScore)).filter((n) => !Number.isNaN(n));
  if (mins.length > 0 && Math.min(...mins) !== 0) issues.push("The lowest band must start at 0.");
  return [...new Set(issues)];
}

export function toPayload(components: readonly ComponentRow[], bands: readonly BandRow[]): { components: ComponentInput[]; bands: GradeBand[] } {
  return {
    components: components.map((c) => ({ id: c.id, name: c.name.trim(), maxScore: Number(c.maxScore), isActive: c.isActive })),
    bands: [...bands]
      .map((b) => ({ grade: b.grade.trim(), minScore: Number(b.minScore), maxScore: Number(b.maxScore), remark: b.remark.trim() }))
      .sort((a, b) => b.minScore - a.minScore),
  };
}

export function move<T>(list: readonly T[], from: number, to: number): T[] {
  if (to < 0 || to >= list.length || from === to) return [...list];
  const next = [...list];
  const [item] = next.splice(from, 1);
  next.splice(to, 0, item);
  return next;
}
