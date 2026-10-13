import { describe, expect, it } from "vitest";
import { MAX_PHOTO_BYTES, checkPhotoFile, photoApiPath, photoUrl } from "./photos";

const id = "01a131b3-b62a-776b-b7f7-3698f51d8986";

describe("photo proxy paths", () => {
  it("only proxies student/staff photos for a real id", () => {
    expect(photoApiPath("students", id)).toBe(`/api/students/${id}/photo`);
    expect(photoApiPath("staff", id)).toBe(`/api/staff/${id}/photo`);
    expect(photoApiPath("guardians", id)).toBeNull();
    expect(photoApiPath("students", "../../auth/me")).toBeNull();
  });

  it("versions the image URL and has none without a photo", () => {
    expect(photoUrl("students", id, "abc 1")).toBe(`/api/media/students/${id}/photo?v=abc%201`);
    expect(photoUrl("staff", id, null)).toBeNull();
  });
});

describe("photo file pre-check", () => {
  const f = (name: string, type: string, size = 1000) => ({ name, type, size });
  it("accepts JPEG, PNG and WebP up to 12 MB", () => {
    expect(checkPhotoFile(f("a.jpg", "image/jpeg"))).toBeNull();
    expect(checkPhotoFile(f("a.png", "image/png"))).toBeNull();
    expect(checkPhotoFile(f("a.webp", "image/webp", MAX_PHOTO_BYTES))).toBeNull();
  });
  it("explains HEIC, other types, size and empty files", () => {
    expect(checkPhotoFile(f("IMG_0001.HEIC", ""))).toMatch(/HEIC/);
    expect(checkPhotoFile(f("a.gif", "image/gif"))).toMatch(/JPEG, PNG or WebP/);
    expect(checkPhotoFile(f("a.jpg", "image/jpeg", MAX_PHOTO_BYTES + 1))).toMatch(/12 MB/);
    expect(checkPhotoFile(f("a.jpg", "image/jpeg", 0))).toMatch(/empty/);
  });
});
