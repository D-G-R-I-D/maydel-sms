import type { GradeBand, GradeSheet, GradeSheetEntry, GradeSheetRow } from "@/lib/api/types";

/**
 * Pure logic for the score grid (E5.1). The grid keeps raw text per cell (so "7." or "" mid-typing
 * is fine), validates against each component's max, and autosaves only the rows that changed.
 */

export type Component = { id: string; name: string; maxScore: number };
/** Raw, as typed. Scores keyed by componentId. */
export type RowDraft = { scores: Record<string, string>; comment: string };
export type Drafts = Record<string, RowDraft>;

export const COMMENT_MAX = 1000;

/** "12.5" → 12.5; "" → null; anything else → error text. */
export function parseScore(text: string, max: number): { value: number | null; error: string | null } {
  const t = text.trim();
  if (t === "") return { value: null, error: null };
  if (!/^\d+(\.\d{1,2})?$/.test(t)) {
    return { value: null, error: /^\d+\.\d{3,}$/.test(t) ? "Use at most 2 decimal places." : "Enter a number." };
  }
  const value = Number(t);
  if (value > max) return { value: null, error: `Max is ${formatScore(max)}.` };
  return { value, error: null };
}

export function formatScore(n: number | null | undefined): string {
  if (n === null || n === undefined) return "";
  return Number.isInteger(n) ? String(n) : String(Math.round(n * 100) / 100);
}

export function draftFromRow(row: GradeSheetRow): RowDraft {
  return {
    scores: Object.fromEntries(row.scores.map((s) => [s.componentId, formatScore(s.score)])),
    comment: row.comment ?? "",
  };
}

export function draftsFromSheet(sheet: GradeSheet): Drafts {
  return Object.fromEntries(sheet.rows.map((r) => [r.studentId, draftFromRow(r)]));
}

/** Errors per componentId (only invalid cells appear). */
export function rowErrors(draft: RowDraft, components: readonly Component[]): Record<string, string> {
  const out: Record<string, string> = {};
  for (const c of components) {
    const { error } = parseScore(draft.scores[c.id] ?? "", c.maxScore);
    if (error) out[c.id] = error;
  }
  if (draft.comment.length > COMMENT_MAX) out.comment = `Keep comments under ${COMMENT_MAX} characters.`;
  return out;
}

/** Live total from valid cells; null when every cell is empty. Rounded to 2 decimals. */
export function rowTotal(draft: RowDraft, components: readonly Component[]): number | null {
  let sum = 0;
  let any = false;
  for (const c of components) {
    const { value } = parseScore(draft.scores[c.id] ?? "", c.maxScore);
    if (value !== null) {
      sum += value;
      any = true;
    }
  }
  return any ? Math.round(sum * 100) / 100 : null;
}

/** Band for a total: bands come highest first; the first whose min the total reaches. */
export function gradeFor(total: number | null, bands: readonly GradeBand[]): GradeBand | null {
  if (total === null) return null;
  const sorted = [...bands].sort((a, b) => b.minScore - a.minScore);
  return sorted.find((b) => total >= b.minScore) ?? null;
}

function sameDraft(a: RowDraft, b: RowDraft, components: readonly Component[]): boolean {
  if (a.comment.trim() !== b.comment.trim()) return false;
  return components.every((c) => {
    const x = parseScore(a.scores[c.id] ?? "", c.maxScore);
    const y = parseScore(b.scores[c.id] ?? "", c.maxScore);
    // Compare numerically where both parse ("7" vs "7.0"), else compare text.
    return x.error || y.error ? (a.scores[c.id] ?? "").trim() === (b.scores[c.id] ?? "").trim() : x.value === y.value;
  });
}

/** Student ids whose draft differs from the last saved state. */
export function changedRows(drafts: Drafts, saved: Drafts, components: readonly Component[]): string[] {
  return Object.keys(drafts).filter((id) => !saved[id] || !sameDraft(drafts[id], saved[id], components));
}

/**
 * The PUT body entries for some rows, in order. Rows with invalid cells are left out (they can't
 * be saved until fixed). An all-empty row is sent as nulls, which clears that student's entry.
 */
export function buildEntries(ids: readonly string[], drafts: Drafts, components: readonly Component[]): GradeSheetEntry[] {
  return ids
    .filter((id) => drafts[id] && Object.keys(rowErrors(drafts[id], components)).length === 0)
    .map((id) => ({
      studentId: id,
      scores: components.map((c) => ({ componentId: c.id, score: parseScore(drafts[id].scores[c.id] ?? "", c.maxScore).value })),
      comment: drafts[id].comment.trim() === "" ? null : drafts[id].comment.trim(),
    }));
}

/** Cell key used for server errors: `${studentId}:${componentId}`, `${studentId}:comment` or `${studentId}:row`. */
export type CellKey = string;

/**
 * Maps 400 `errors` keys from the API back to cells. Keys look like `entries[3].scores.{componentId}`,
 * `entries[3].comment` or `entries[3].studentId`; the index is into the request's `entries`.
 */
export function mapServerErrors(errors: Record<string, string[]> | undefined, entries: readonly GradeSheetEntry[]): Record<CellKey, string> {
  const out: Record<CellKey, string> = {};
  for (const [key, messages] of Object.entries(errors ?? {})) {
    const m = /^entries\[(\d+)\](?:\.(scores)\.([^.]+)|\.(comment)|\.(\w+))?$/i.exec(key);
    const entry = m ? entries[Number(m[1])] : undefined;
    if (!m || !entry) continue;
    const cell = m[3] ? `${entry.studentId}:${m[3]}` : m[4] ? `${entry.studentId}:comment` : `${entry.studentId}:row`;
    out[cell] = messages.join(" ");
  }
  return out;
}

/** Where a key press moves focus. `editable(r)` skips locked rows for vertical moves. */
export function nextCell(
  pos: { row: number; col: number },
  move: "left" | "right" | "up" | "down",
  size: { rows: number; cols: number },
  editable: (row: number) => boolean = () => true,
): { row: number; col: number } | null {
  const { row, col } = pos;
  if (move === "left") return col > 0 ? { row, col: col - 1 } : null;
  if (move === "right") return col < size.cols - 1 ? { row, col: col + 1 } : null;
  const step = move === "down" ? 1 : -1;
  for (let r = row + step; r >= 0 && r < size.rows; r += step) if (editable(r)) return { row: r, col };
  return null;
}

/** sessionStorage backup of unsaved edits, per sheet. */
export function backupKey(classRoomId: string, subjectId: string, termId: string): string {
  return `maydel.gradesheet.v1.${classRoomId}.${subjectId}.${termId}`;
}

export type Backup = { at: number; rows: Drafts };

export function parseBackup(raw: string | null): Backup | null {
  if (!raw) return null;
  try {
    const v: unknown = JSON.parse(raw);
    if (typeof v !== "object" || v === null || !("rows" in v) || !("at" in v)) return null;
    const rows = (v as Backup).rows;
    if (typeof rows !== "object" || rows === null) return null;
    const clean: Drafts = {};
    for (const [id, d] of Object.entries(rows)) {
      if (d && typeof d === "object" && typeof d.comment === "string" && d.scores && typeof d.scores === "object") {
        clean[id] = { comment: d.comment, scores: Object.fromEntries(Object.entries(d.scores).filter(([, s]) => typeof s === "string")) as Record<string, string> };
      }
    }
    return { at: Number((v as Backup).at) || 0, rows: clean };
  } catch {
    return null;
  }
}
