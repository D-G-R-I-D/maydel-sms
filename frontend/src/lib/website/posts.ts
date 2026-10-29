import type { components } from "@/lib/api/schema";

/** E7/E12.1 posts: news and notices in one model (drafts until published). */
export type Post = components["schemas"]["PostDto"];
export type PostInput = components["schemas"]["PostInput"];
export type SiteEvent = components["schemas"]["EventDto"];
export type SiteEventInput = components["schemas"]["EventInput"];

/** Who sees a post or event; the API sends flags as "Public, Parents". */
export const audiences = [
  { value: "Public", label: "Website (public)" },
  { value: "Parents", label: "Parents" },
  { value: "Students", label: "Students" },
  { value: "Staff", label: "Staff" },
] as const;
export type AudienceFlag = (typeof audiences)[number]["value"];

export function parseAudience(s: string | null | undefined): AudienceFlag[] {
  const parts = (s ?? "").split(",").map((p) => p.trim());
  if (parts.includes("Everyone")) return audiences.map((a) => a.value);
  return audiences.map((a) => a.value).filter((v) => parts.includes(v));
}

export function formatAudience(flags: readonly AudienceFlag[]): string {
  return audiences.map((a) => a.value).filter((v) => flags.includes(v)).join(", ");
}

/** "Website, Parents" for lists. */
export function audienceLabel(s: string): string {
  const flags = parseAudience(s);
  if (flags.length === audiences.length) return "Everyone";
  return flags.map((f) => (f === "Public" ? "Website" : f)).join(", ") || "Nobody";
}

/** True when the editor's HTML has no words and no pictures. */
export function isBlankHtml(html: string): boolean {
  return !/<img\b/i.test(html) && html.replace(/<[^>]*>/g, "").replace(/&nbsp;/g, " ").trim() === "";
}

const linkOk = (v: string) => /^https?:\/\/\S+$/i.test(v) || /^\/(?!\/)\S*$/.test(v);

export type PostForm = {
  title: string;
  summary: string;
  bodyHtml: string;
  category: string;
  audience: AudienceFlag[];
  isPinned: boolean;
  showFrom: string;
  showUntil: string;
  isFeatured: boolean;
  linkUrl: string;
  linkText: string;
  inTicker: boolean;
  inCarousel: boolean;
};

/** The post editor's rules, mirroring the server's (news.title, news.body, news.audience, news.window, news.link, news.public). */
export function postErrors(f: PostForm, hasPicture: boolean): Partial<Record<keyof PostForm, string>> {
  const e: Partial<Record<keyof PostForm, string>> = {};
  if (!f.title.trim()) e.title = "Give it a title.";
  else if (f.title.length > 200) e.title = "200 characters at most.";
  if (f.summary.length > 500) e.summary = "500 characters at most.";
  if (f.category.length > 60) e.category = "60 characters at most.";
  if (isBlankHtml(f.bodyHtml)) e.bodyHtml = "Write the content.";
  if (f.audience.length === 0) e.audience = "Choose who sees it.";
  else if ((f.inTicker || f.inCarousel || f.isFeatured) && !f.audience.includes("Public")) e.audience = "The carousel, ticker and pop-up are on the website, so include Website (public).";
  if (f.showFrom && f.showUntil && f.showUntil < f.showFrom) e.showUntil = "The end must be on or after the start.";
  if (f.linkUrl.trim() && !linkOk(f.linkUrl.trim())) e.linkUrl = "A web address (https://…) or a page on the site (/admissions-info).";
  if (f.linkText.length > 60) e.linkText = "60 characters at most.";
  if (f.inCarousel && !hasPicture) e.inCarousel = "The homepage carousel needs a picture. Save, then add one.";
  return e;
}

/** "2026-10-24T09:00" (a datetime-local in WAT) ↔ the API's instants. */
export function toLocalInput(iso: string | null | undefined): string {
  if (!iso) return "";
  return new Date(new Date(iso).getTime() + 3600 * 1000).toISOString().slice(0, 16);
}
export function fromLocalInput(v: string): string | null {
  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(v) ? `${v}:00+01:00` : null;
}

export function postToForm(p: Post | null): PostForm {
  return {
    title: p?.title ?? "",
    summary: p?.summary ?? "",
    bodyHtml: p?.bodyHtml ?? (p?.body ? p.body.split(/\n\s*\n/).map((para) => `<p>${escapeHtml(para.trim()).replace(/\n/g, "<br>")}</p>`).join("") : ""),
    category: p?.category ?? "",
    audience: p ? parseAudience(p.audience) : ["Public", "Parents"],
    isPinned: p?.isPinned ?? false,
    showFrom: toLocalInput(p?.showFrom),
    showUntil: toLocalInput(p?.showUntil),
    isFeatured: p?.isFeatured ?? false,
    linkUrl: p?.linkUrl ?? "",
    linkText: p?.linkText ?? "",
    inTicker: p?.inTicker ?? false,
    inCarousel: p?.inCarousel ?? false,
  };
}

export function formToInput(f: PostForm): PostInput {
  const opt = (v: string) => (v.trim() ? v.trim() : null);
  return {
    title: f.title.trim(),
    summary: opt(f.summary),
    body: "",
    bodyHtml: f.bodyHtml,
    category: opt(f.category),
    audience: formatAudience(f.audience),
    isPinned: f.isPinned,
    showFrom: fromLocalInput(f.showFrom),
    showUntil: fromLocalInput(f.showUntil),
    isFeatured: f.isFeatured,
    linkUrl: opt(f.linkUrl),
    linkText: opt(f.linkText),
    inTicker: f.inTicker,
    inCarousel: f.inCarousel,
  };
}

export function escapeHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

export type EventForm = { title: string; descriptionHtml: string; startsAt: string; endsAt: string; location: string; audience: AudienceFlag[] };

export function eventToForm(e: SiteEvent | null): EventForm {
  return {
    title: e?.title ?? "",
    descriptionHtml: e?.descriptionHtml ?? (e?.description ? `<p>${escapeHtml(e.description).replace(/\n/g, "<br>")}</p>` : ""),
    startsAt: toLocalInput(e?.startsAt),
    endsAt: toLocalInput(e?.endsAt),
    location: e?.location ?? "",
    audience: e ? parseAudience(e.audience) : ["Public", "Parents", "Students"],
  };
}

export function eventErrors(f: EventForm): Partial<Record<keyof EventForm, string>> {
  const e: Partial<Record<keyof EventForm, string>> = {};
  if (!f.title.trim()) e.title = "Give the event a title.";
  else if (f.title.length > 200) e.title = "200 characters at most.";
  if (!fromLocalInput(f.startsAt)) e.startsAt = "Choose when it starts.";
  if (f.endsAt && f.startsAt && f.endsAt < f.startsAt) e.endsAt = "The event must end after it starts.";
  if (f.location.length > 200) e.location = "200 characters at most.";
  if (f.audience.length === 0) e.audience = "Choose who sees it.";
  return e;
}

export function eventToInput(f: EventForm): SiteEventInput {
  return {
    title: f.title.trim(),
    description: null,
    descriptionHtml: isBlankHtml(f.descriptionHtml) ? null : f.descriptionHtml,
    startsAt: fromLocalInput(f.startsAt)!,
    endsAt: fromLocalInput(f.endsAt),
    location: f.location.trim() || null,
    audience: formatAudience(f.audience),
  };
}

/** Upcoming events (not yet over, soonest first) or past ones (newest first). */
export function splitEvents(events: SiteEvent[], past: boolean, now = Date.now()): SiteEvent[] {
  const over = (e: SiteEvent) => new Date(e.endsAt ?? e.startsAt).getTime() < now;
  return past ? events.filter(over).reverse() : events.filter((e) => !over(e));
}
