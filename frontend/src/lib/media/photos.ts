/** Student & staff photos (ADR-006). Shared by the BFF media route and the UI. */

export const MAX_PHOTO_BYTES = 12 * 1024 * 1024;
export const PHOTO_ACCEPT = "image/jpeg,image/png,image/webp";
const ACCEPTED = new Set(PHOTO_ACCEPT.split(","));

/** "portal" = a parent's/student's own child, via the portal API (read-only). */
export type PhotoKind = "students" | "staff" | "portal";

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** The API path for a photo, or null for anything we don't proxy (no open proxy through the BFF). */
export function photoApiPath(kind: string, id: string): string | null {
  if (!guid.test(id)) return null;
  if (kind === "portal") return `/api/portal/children/${id}/photo`;
  if (kind !== "students" && kind !== "staff") return null;
  return `/api/${kind}/${id}/photo`;
}

/** Same-origin image URL; `?v=` changes when the photo is replaced, so the private cache is safe. */
export function photoUrl(kind: PhotoKind, id: string, version: string | null | undefined): string | null {
  return version ? `/api/media/${kind}/${id}/photo?v=${encodeURIComponent(version)}` : null;
}

/** Client-side pre-check mirroring the API (JPEG/PNG/WebP, ≤ 12 MB). Returns an error message or null. */
export function checkPhotoFile(file: { type: string; size: number; name: string }): string | null {
  const heic = /\.(heic|heif)$/i.test(file.name) || /heic|heif/i.test(file.type);
  if (heic) return "iPhone HEIC photos aren't accepted. Share it as JPEG (choose “Most Compatible”) and try again.";
  if (!ACCEPTED.has(file.type)) return "Upload a JPEG, PNG or WebP photo.";
  if (file.size > MAX_PHOTO_BYTES) return "The photo must be 12 MB or smaller.";
  if (file.size === 0) return "That file is empty.";
  return null;
}
