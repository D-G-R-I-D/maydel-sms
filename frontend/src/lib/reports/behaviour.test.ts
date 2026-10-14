import { describe, expect, it } from "vitest";
import { behaviourPayload, clubErrors, clubsFromCard, newClub, ratedCount, ratingsFromCard } from "./behaviour";

describe("behaviour & clubs", () => {
  it("reads ratings and clubs from a report card", () => {
    expect(ratingsFromCard([{ traitId: "t1", trait: "Punctual", rating: "A" }, { traitId: "t2", trait: "Attentive", rating: null }])).toEqual({ t1: "A", t2: null });
    expect(clubsFromCard([{ name: "Coding", rating: "Excellent" }, { name: "Chess", rating: null }]).map((c) => [c.name, c.rating])).toEqual([
      ["Coding", "Excellent"],
      ["Chess", ""],
    ]);
  });

  it("sends every trait (null when unrated) and drops blank clubs", () => {
    const clubs = [newClub("  Coding ", " Excellent "), newClub("", ""), newClub("Chess", "")];
    expect(behaviourPayload(["t1", "t2"], { t1: "B" }, clubs)).toEqual({
      ratings: [
        { traitId: "t1", rating: "B" },
        { traitId: "t2", rating: null },
      ],
      clubs: [
        { name: "Coding", rating: "Excellent" },
        { name: "Chess", rating: null },
      ],
    });
  });

  it("checks clubs like the API: max, lengths, duplicates, rating without name", () => {
    const a = newClub("Coding");
    const b = newClub("coding");
    const c = newClub("", "Good");
    const d = newClub("x".repeat(41));
    const errs = clubErrors([a, b, c, d], 6);
    expect(errs[b.key]).toBe("This club is listed twice.");
    expect(errs[c.key]).toBe("Add the club name.");
    expect(errs[d.key]).toMatch(/under 41/);
    expect(errs[a.key]).toBeUndefined();
    expect(clubErrors(Array.from({ length: 7 }, (_, i) => newClub(`Club ${i}`)), 6)._list).toBe("At most 6 clubs or activities.");
  });

  it("counts rated traits", () => {
    expect(ratedCount({ a: "A", b: null, c: "C" })).toBe(2);
  });
});

describe("trait list rules", () => {
  it("needs unique, non-empty names", async () => {
    const { traitIssues } = await import("./behaviour");
    expect(traitIssues([{ name: "Punctual" }, { name: "Attentive" }])).toEqual([]);
    expect(traitIssues([{ name: "Punctual" }, { name: " punctual " }, { name: "" }])).toEqual(['"punctual" appears twice.', "Every trait needs a name."]);
  });
});
