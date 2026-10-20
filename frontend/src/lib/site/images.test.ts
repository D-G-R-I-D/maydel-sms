import { describe, expect, it } from "vitest";
import { siteImage } from "./images";

describe("website images", () => {
  it("maps the API's public image URL to the versioned same-origin proxy", () => {
    const id = "01a131b3-b24d-7d20-bde5-fdeffa6b31ca";
    expect(siteImage(`/api/public/site/images/news/${id}?v=3f9a`)).toBe(`/api/site/images/news/${id}/3f9a`);
    expect(siteImage(`http://api:8080/api/public/site/images/gallery/${id}?v=20261019T1200`)).toBe(`/api/site/images/gallery/${id}/20261019T1200`);
    expect(siteImage(`/api/public/site/images/staff/${id}`)).toBe(`/api/site/images/staff/${id}/0`);
    expect(siteImage("/api/students/x/photo")).toBeNull();
    expect(siteImage(null)).toBeNull();
  });
});
