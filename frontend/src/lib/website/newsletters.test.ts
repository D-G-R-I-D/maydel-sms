import { describe, expect, it } from "vitest";
import { formToNewsletterInput, newsletterAudienceLabel, newsletterErrors, newsletterTemplates, newsletterToForm, parseNewsletterAudience, scheduleAt } from "./newsletters";

describe("newsletter helpers", () => {
  it("reads and labels audience flags", () => {
    expect(parseNewsletterAudience("Parents, Subscribers")).toEqual(["Parents", "Subscribers"]);
    expect(newsletterAudienceLabel("Staff, Subscribers")).toBe("Staff, Website subscribers");
  });

  it("sends a section or class only when parents are included", () => {
    const f = { ...newsletterToForm(null), title: "Hi", bodyHtml: "<p>x</p>", scope: { kind: "section" as const, section: "Primary" as const } };
    expect(formToNewsletterInput(f)).toMatchObject({ audience: "Parents", section: "Primary", classRoomId: null });
    expect(formToNewsletterInput({ ...f, audience: ["Staff"] })).toMatchObject({ audience: "Staff", section: null, classRoomId: null });
  });

  it("mirrors the server's limits", () => {
    const f = { ...newsletterToForm(null), title: "Hi", bodyHtml: "<p>x</p>" };
    expect(newsletterErrors(f)).toEqual({});
    expect(newsletterErrors({ ...f, title: "x".repeat(151) }).title).toBeDefined();
    expect(newsletterErrors({ ...f, audience: [] }).audience).toBeDefined();
    expect(newsletterErrors({ ...f, bodyHtml: "<p></p>" }).bodyHtml).toBeDefined();
  });

  it("builds template links as full addresses on the site", () => {
    const t = newsletterTemplates("https://maydel.example");
    expect(t.map((x) => x.id)).toEqual(["termly", "event", "announcement"]);
    expect(t.every((x) => !/href="\//.test(x.html))).toBe(true);
    expect(t[0].html).toContain('href="https://maydel.example/news"');
  });

  it("schedules in Abuja time", () => {
    expect(scheduleAt("2026-11-02T07:30")).toBe("2026-11-02T07:30:00+01:00");
    expect(scheduleAt("")).toBeNull();
  });
});
