import { describe, expect, it } from "vitest";
import { answerBody, formatRemaining, isAnswered, listNumbers, timerAnnouncement, timerTone, unanswered, windowLabel, type PaperItem } from "./attempt";

const item = (order: number, kind: PaperItem["kind"], answered: Partial<PaperItem> = {}): PaperItem => ({
  id: `i${order}`,
  order,
  section: 0,
  kind,
  text: "Q",
  imageUrl: null,
  marks: 1,
  options: [],
  answerOptionIds: [],
  answerTrueFalse: null,
  answerText: null,
  answered: false,
  score: null,
  comment: null,
  ...answered,
});

describe("sitting a test", () => {
  it("sends only the field each kind uses", () => {
    const a = { optionIds: ["o1"], trueFalse: true, text: "x" };
    expect(answerBody("Objective", a)).toEqual({ optionIds: ["o1"], trueFalse: null, text: null });
    expect(answerBody("TrueFalse", a)).toEqual({ optionIds: null, trueFalse: true, text: null });
    expect(answerBody("Theory", { ...a, text: "y".repeat(6000) }).text).toHaveLength(5000);
    expect(answerBody("MultiSelect", { optionIds: null, trueFalse: null, text: null }).optionIds).toEqual([]);
  });

  it("knows what counts as answered and lists the rest", () => {
    expect(isAnswered("ShortAnswer", { optionIds: null, trueFalse: null, text: "  " })).toBe(false);
    expect(isAnswered("TrueFalse", { optionIds: null, trueFalse: false, text: null })).toBe(true);
    const items = [item(0, "Objective", { answerOptionIds: ["o"] }), item(1, "TrueFalse"), item(2, "Theory")];
    expect(unanswered(items, { i3: { optionIds: null, trueFalse: null, text: null }, i2: { optionIds: null, trueFalse: null, text: "Done" } })).toEqual([2]);
    expect(listNumbers([3, 7, 12])).toBe("3, 7 and 12");
    expect(listNumbers([1, 2, 3, 4], 2)).toBe("1, 2 and 2 more");
  });

  it("formats and colours the countdown, and announces 5 and 1 minutes", () => {
    expect(formatRemaining(2700)).toBe("45:00");
    expect(formatRemaining(3909)).toBe("1:05:09");
    expect(formatRemaining(-4)).toBe("00:00");
    expect([timerTone(301), timerTone(299), timerTone(59)]).toEqual(["normal", "warning", "danger"]);
    expect(timerAnnouncement(301, 300)).toBe("5 minutes left.");
    expect(timerAnnouncement(61, 59)).toBe("1 minute left.");
    expect(timerAnnouncement(200, 199)).toBeNull();
  });

  it("labels the window", () => {
    const now = Date.parse("2026-10-19T08:00:00Z");
    expect(windowLabel({ state: "Upcoming", opensAt: "2026-10-19T10:00:00Z", closesAt: "2026-10-19T16:00:00Z" }, now)).toBe("Opens in 2h");
    expect(windowLabel({ state: "Open", opensAt: "2026-10-19T07:00:00Z", closesAt: "2026-10-19T08:25:00Z" }, now)).toBe("Closes in 25m");
    expect(windowLabel({ state: "Missed", opensAt: "2026-10-18T07:00:00Z", closesAt: "2026-10-18T08:00:00Z" }, now)).toMatch(/^Closed /);
  });
});
