import { describe, expect, it } from "vitest";
import { audienceLabel, eventErrors, eventToForm, formatAudience, formToInput, fromLocalInput, isBlankHtml, parseAudience, postErrors, postToForm, toLocalInput } from "./posts";
import { apiImageSrc, rewriteContentImages } from "@/lib/site/images";

const ID = "3f2c1a9e-8b7d-4c6e-9a1b-2c3d4e5f6a7b";

describe("post and event helpers", () => {
  it("reads and writes audience flags", () => {
    expect(parseAudience("Public, Parents")).toEqual(["Public", "Parents"]);
    expect(parseAudience("Everyone")).toEqual(["Public", "Parents", "Students", "Staff"]);
    expect(formatAudience(["Parents", "Public"])).toBe("Public, Parents");
    expect(audienceLabel("Public, Staff")).toBe("Website, Staff");
    expect(audienceLabel("Everyone")).toBe("Everyone");
  });

  it("treats an editor with no words or pictures as empty", () => {
    expect(isBlankHtml("<p></p>")).toBe(true);
    expect(isBlankHtml("<p>&nbsp;</p>")).toBe(true);
    expect(isBlankHtml(`<figure><img src="/x"></figure>`)).toBe(false);
    expect(isBlankHtml("<p>Hi</p>")).toBe(false);
  });

  it("mirrors the server's post rules", () => {
    const f = { ...postToForm(null), title: "Open day", bodyHtml: "<p>Come along</p>" };
    expect(postErrors(f, false)).toEqual({});
    expect(postErrors({ ...f, inTicker: true, audience: ["Parents"] }, false).audience).toMatch(/Website/);
    expect(postErrors({ ...f, inCarousel: true }, false).inCarousel).toMatch(/picture/);
    expect(postErrors({ ...f, inCarousel: true }, true).inCarousel).toBeUndefined();
    expect(postErrors({ ...f, linkUrl: "javascript:alert(1)" }, false).linkUrl).toBeDefined();
    expect(postErrors({ ...f, linkUrl: "/admissions/apply" }, false).linkUrl).toBeUndefined();
    expect(postErrors({ ...f, showFrom: "2026-11-02T08:00", showUntil: "2026-11-01T08:00" }, false).showUntil).toBeDefined();
  });

  it("sends formatted HTML with an empty plain body", () => {
    const input = formToInput({ ...postToForm(null), title: " Open day ", bodyHtml: "<p>Hi</p>", summary: " " });
    expect(input).toMatchObject({ title: "Open day", body: "", bodyHtml: "<p>Hi</p>", summary: null, audience: "Public, Parents" });
  });

  it("turns an old plain-text post into paragraphs for the editor", () => {
    const f = postToForm({ body: "One <b>\n\nTwo", bodyHtml: null } as never);
    expect(f.bodyHtml).toBe("<p>One &lt;b&gt;</p><p>Two</p>");
  });

  it("converts WAT datetime-local values", () => {
    expect(fromLocalInput("2026-11-02T09:30")).toBe("2026-11-02T09:30:00+01:00");
    expect(toLocalInput("2026-11-02T08:30:00Z")).toBe("2026-11-02T09:30");
    expect(fromLocalInput("")).toBeNull();
  });

  it("checks events", () => {
    const f = { ...eventToForm(null), title: "Sports day", startsAt: "2026-11-20T09:00" };
    expect(eventErrors(f)).toEqual({});
    expect(eventErrors({ ...f, endsAt: "2026-11-20T08:00" }).endsAt).toBeDefined();
  });

  it("points content pictures at the site proxy and back", () => {
    const html = `<figure class="wide"><img src="/api/public/site/images/media/${ID}?v=abc12" alt="x"></figure><img src="https://evil.example/p.gif">`;
    expect(rewriteContentImages(html)).toContain(`src="/api/site/images/media/${ID}/abc12"`);
    expect(rewriteContentImages(html)).toContain(`src="https://evil.example/p.gif"`);
    expect(apiImageSrc(`/api/site/images/media/${ID}/abc12`)).toBe(`/api/public/site/images/media/${ID}?v=abc12`);
  });
});
