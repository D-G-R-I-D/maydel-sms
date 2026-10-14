import { describe, expect, it } from "vitest";
import { DRAFT_OWNER_KEY, DRAFT_TTL_MS, claimDrafts, clearDrafts, draftKey, readDraft, removeDraft, writeDraft } from "./store";

function memory() {
  const m = new Map<string, string>();
  return {
    getItem: (k: string) => m.get(k) ?? null,
    setItem: (k: string, v: string) => void m.set(k, v),
    removeItem: (k: string) => void m.delete(k),
    key: (i: number) => [...m.keys()][i] ?? null,
    get length() {
      return m.size;
    },
    keys: () => [...m.keys()],
  };
}

describe("drafts store", () => {
  it("namespaces keys by user", () => {
    expect(draftKey("u1", "comment", "s1", "t1")).toBe("draft:u1:comment:s1:t1");
  });

  it("round-trips a draft with its base", () => {
    const s = memory();
    writeDraft("draft:u1:x", { a: 1 }, "base", 1000, s);
    expect(readDraft("draft:u1:x", 2000, s)).toEqual({ value: { a: 1 }, savedAt: 1000, base: "base" });
    removeDraft("draft:u1:x", s);
    expect(readDraft("draft:u1:x", 2000, s)).toBeNull();
  });

  it("drops drafts older than the TTL", () => {
    const s = memory();
    writeDraft("draft:u1:x", "v", null, 0, s);
    expect(readDraft("draft:u1:x", DRAFT_TTL_MS + 1, s)).toBeNull();
    expect(s.length).toBe(0);
  });

  it("ignores corrupt entries and missing storage", () => {
    const s = memory();
    s.setItem("draft:u1:x", "{not json");
    expect(readDraft("draft:u1:x", 0, s)).toBeNull();
    expect(readDraft("draft:u1:x", 0, null)).toBeNull();
    expect(() => writeDraft("k", 1, null, 0, null)).not.toThrow();
  });

  it("clears only drafts on sign-out", () => {
    const s = memory();
    writeDraft("draft:u1:a", 1, null, 0, s);
    s.setItem("theme", "dark");
    clearDrafts({}, s);
    expect(s.keys()).toEqual(["theme"]);
  });

  it("a different user claiming the tab wipes the previous user's drafts", () => {
    const s = memory();
    claimDrafts("u1", s);
    writeDraft("draft:u1:a", 1, null, Date.now(), s);
    claimDrafts("u1", s);
    expect(readDraft("draft:u1:a", Date.now(), s)).not.toBeNull();
    claimDrafts("u2", s);
    expect(s.keys()).toEqual([DRAFT_OWNER_KEY]);
    expect(s.getItem(DRAFT_OWNER_KEY)).toBe("u2");
  });
});
