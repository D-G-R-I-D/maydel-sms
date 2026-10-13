/** Secrets are masked by the API as this literal. */
export const REDACTED = "***";

export type DiffValue = { kind: "absent" } | { kind: "redacted" } | { kind: "null" } | { kind: "value"; text: string };
export type DiffRow = { field: string; before: DiffValue; after: DiffValue; changed: boolean };

const absent: DiffValue = { kind: "absent" };

function toValue(v: unknown): DiffValue {
  if (v === REDACTED) return { kind: "redacted" };
  if (v === null || v === undefined) return { kind: "null" };
  if (typeof v === "string") return { kind: "value", text: v };
  if (typeof v === "number" || typeof v === "boolean") return { kind: "value", text: String(v) };
  return { kind: "value", text: JSON.stringify(v) };
}

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === "object" && v !== null && !Array.isArray(v);
}

function isFromTo(v: unknown): v is { from: unknown; to: unknown } {
  return isRecord(v) && "from" in v && "to" in v && Object.keys(v).length === 2;
}

/**
 * Turns an audit entry's `changes` into before/after rows.
 * Added → { Prop: value } (after only); Deleted → { Prop: value } (before only);
 * Modified → { Prop: { from, to } }.
 */
export function diffRows(action: string, changes: unknown): DiffRow[] {
  if (!isRecord(changes)) return [];
  return Object.entries(changes).map(([field, raw]) => {
    if (action === "Added") return { field, before: absent, after: toValue(raw), changed: true };
    if (action === "Deleted") return { field, before: toValue(raw), after: absent, changed: true };
    // A secret that changed is masked as a whole: both sides are redacted.
    if (raw === REDACTED) return { field, before: { kind: "redacted" }, after: { kind: "redacted" }, changed: true };
    if (isFromTo(raw)) {
      const before = toValue(raw.from);
      const after = toValue(raw.to);
      return { field, before, after, changed: JSON.stringify(before) !== JSON.stringify(after) };
    }
    return { field, before: absent, after: toValue(raw), changed: true };
  });
}
