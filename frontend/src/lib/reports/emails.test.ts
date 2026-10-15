import { describe, expect, it } from "vitest";
import { emailCounts, hasPendingEmails, noGuardianEmails, publishToast } from "./emails";

describe("report emails", () => {
  it("summarises the publish result", () => {
    expect(publishToast({ published: 5, emailsQueued: 7 }, true)).toBe("Published 5 reports · 7 emails queued");
    expect(publishToast({ published: 1, emailsQueued: 1 }, true)).toBe("Published 1 report · 1 email queued");
    expect(publishToast({ published: 3, emailsQueued: 0 }, false)).toBe("Published 3 reports");
  });

  it("flags a ticked box with no guardian addresses", () => {
    expect(noGuardianEmails({ published: 3, emailsQueued: 0 }, true)).toBe(true);
    expect(noGuardianEmails({ published: 3, emailsQueued: 0 }, false)).toBe(false);
    expect(noGuardianEmails({ published: 0, emailsQueued: 0 }, true)).toBe(false);
    expect(noGuardianEmails({ published: 3, emailsQueued: 2 }, true)).toBe(false);
  });

  it("polls only while something is pending, and counts by status", () => {
    expect(hasPendingEmails([{ status: "Sent" }, { status: "Failed" }])).toBe(false);
    expect(hasPendingEmails([{ status: "Sent" }, { status: "Pending" }])).toBe(true);
    expect(emailCounts([{ status: "Sent" }, { status: "Sent" }, { status: "Failed" }])).toEqual({ Pending: 0, Sent: 2, Failed: 1, Skipped: 0 });
  });
});
