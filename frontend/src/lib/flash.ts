/**
 * One-shot toast messages that survive a redirect. A BFF route sets this short-lived, non-httpOnly
 * cookie; <Toaster> reads it once and deletes it. It never holds anything sensitive.
 */
export const FLASH_COOKIE = "maydel_flash";

export type FlashKind = "signed-in" | "password-changed" | "signed-out";
export type Flash = { kind: FlashKind; name?: string };

type Message = { tone: "success" | "info"; title: string; description?: string };

export const flashMessages: Record<FlashKind, (name?: string) => Message> = {
  "signed-in": (name) => ({ tone: "success", title: name ? `Welcome back, ${name}` : "Welcome back" }),
  "password-changed": () => ({
    tone: "success",
    title: "Password changed",
    description: "You've been signed out on your other devices.",
  }),
  "signed-out": () => ({ tone: "info", title: "You've signed out", description: "See you next time." }),
};

const kinds = Object.keys(flashMessages) as FlashKind[];

export function encodeFlash(flash: Flash): string {
  return JSON.stringify(flash); // the cookie serializer URL-encodes it
}

export function parseFlash(raw: string): Flash | null {
  try {
    const value: unknown = JSON.parse(raw);
    if (typeof value !== "object" || value === null || !("kind" in value)) return null;
    const kind = (value as { kind: unknown }).kind;
    if (typeof kind !== "string" || !(kinds as string[]).includes(kind)) return null;
    const name = "name" in value && typeof value.name === "string" ? value.name.slice(0, 60) : undefined;
    return { kind: kind as FlashKind, name };
  } catch {
    return null;
  }
}

const honorifics = /^(mr|mrs|ms|miss|mx|dr|prof|chief|engr|alhaji|alhaja|hajia|pastor|rev|sir|madam|barr)\.?$/i;

/** How to greet someone: their first name, or "Mrs. Eze" style when the name starts with a title. */
export function firstName(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "";
  if (honorifics.test(parts[0]) && parts.length > 1) {
    const title = parts[0].endsWith(".") ? parts[0] : `${parts[0]}.`;
    return `${title} ${parts[parts.length - 1]}`;
  }
  return parts[0];
}
