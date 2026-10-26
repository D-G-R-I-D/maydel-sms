import type { components } from "@/lib/api/schema";
import { isBlankHtml } from "./posts";

/** E12.6 newsletters: written in the rich editor, sent by email to parents / staff / subscribers. */
export type Newsletter = components["schemas"]["NewsletterDto"];
export type NewsletterSummary = components["schemas"]["NewsletterSummary"];
export type NewsletterInput = components["schemas"]["NewsletterInput"];
export type NewsletterStatus = components["schemas"]["NewsletterStatus"];
export type RecipientCount = components["schemas"]["RecipientCount"];
export type SchoolSection = components["schemas"]["SchoolSection"];

export const NewsletterCodes = { notEditable: "newsletter.not_editable", noRecipients: "newsletter.no_recipients", link: "newsletter.link" } as const;

export const newsletterAudiences = [
  { value: "Parents", label: "Parents" },
  { value: "Staff", label: "Staff" },
  { value: "Subscribers", label: "Website subscribers" },
] as const;
export type NewsletterAudienceFlag = (typeof newsletterAudiences)[number]["value"];

export const sections: { value: SchoolSection; label: string }[] = [
  { value: "Creche", label: "Creche" },
  { value: "Nursery", label: "Nursery" },
  { value: "Primary", label: "Primary" },
  { value: "JuniorSecondary", label: "Junior Secondary" },
  { value: "SeniorSecondary", label: "Senior Secondary" },
];

export function parseNewsletterAudience(s: string | null | undefined): NewsletterAudienceFlag[] {
  const parts = (s ?? "").split(",").map((p) => p.trim());
  if (parts.includes("Everyone") || parts.includes("All")) return newsletterAudiences.map((a) => a.value);
  return newsletterAudiences.map((a) => a.value).filter((v) => parts.includes(v));
}

export function formatNewsletterAudience(flags: readonly NewsletterAudienceFlag[]): string {
  return newsletterAudiences.map((a) => a.value).filter((v) => flags.includes(v)).join(", ");
}

export function newsletterAudienceLabel(s: string): string {
  return parseNewsletterAudience(s)
    .map((v) => newsletterAudiences.find((a) => a.value === v)!.label)
    .join(", ") || "Nobody";
}

export const statusTone: Record<NewsletterStatus, "neutral" | "warning" | "success"> = { Draft: "neutral", Scheduled: "warning", Sent: "success" };

/** Who among the parents: everyone, one section, or one class. */
export type ParentScope = { kind: "all" } | { kind: "section"; section: SchoolSection } | { kind: "class"; classRoomId: string };

export type NewsletterForm = {
  title: string;
  preheader: string;
  bodyHtml: string;
  audience: NewsletterAudienceFlag[];
  scope: ParentScope;
  publishOnWebsite: boolean;
};

export function newsletterToForm(n: Newsletter | null): NewsletterForm {
  return {
    title: n?.title ?? "",
    preheader: n?.preheader ?? "",
    bodyHtml: n?.bodyHtml ?? "",
    audience: n ? parseNewsletterAudience(n.audience) : ["Parents"],
    scope: n?.classRoomId ? { kind: "class", classRoomId: n.classRoomId } : n?.section ? { kind: "section", section: n.section } : { kind: "all" },
    publishOnWebsite: n?.publishOnWebsite ?? true,
  };
}

export function formToNewsletterInput(f: NewsletterForm): NewsletterInput {
  const parents = f.audience.includes("Parents");
  return {
    title: f.title.trim(),
    preheader: f.preheader.trim() || null,
    bodyHtml: f.bodyHtml,
    audience: formatNewsletterAudience(f.audience),
    section: parents && f.scope.kind === "section" ? f.scope.section : null,
    classRoomId: parents && f.scope.kind === "class" ? f.scope.classRoomId : null,
    publishOnWebsite: f.publishOnWebsite,
  };
}

/** Mirrors the server: title ≤ 150, preheader ≤ 200, some content, at least one audience. */
export function newsletterErrors(f: NewsletterForm): Partial<Record<"title" | "preheader" | "bodyHtml" | "audience", string>> {
  const e: Partial<Record<"title" | "preheader" | "bodyHtml" | "audience", string>> = {};
  if (!f.title.trim()) e.title = "Give it a subject line.";
  else if (f.title.length > 150) e.title = "150 characters at most.";
  if (f.preheader.length > 200) e.preheader = "200 characters at most.";
  if (isBlankHtml(f.bodyHtml)) e.bodyHtml = "Write the newsletter.";
  if (f.audience.length === 0) e.audience = "Choose who gets it.";
  return e;
}

/**
 * Ready-made starting points for the editor (the school can change every word). Links are full addresses
 * on `site` (the server keeps http(s) links only, and emails need them absolute).
 */
export const newsletterTemplates = (site: string) => [
  {
    id: "termly",
    label: "Termly newsletter",
    title: "Maydel Spring Schools newsletter",
    preheader: "News, highlights and dates for your diary",
    html: [
      "<h2>A word from the Head</h2>",
      "<p>Dear parents and guardians,</p>",
      "<p>What a term it has been! Thank you for your support. Here is a look at what our pupils have been up to, and the dates coming up.</p>",
      "<h2>Highlights</h2>",
      "<ul><li><p>…</p></li><li><p>…</p></li><li><p>…</p></li></ul>",
      "<h2>Dates for your diary</h2>",
      "<ul><li><p><strong>Date</strong>: event</p></li><li><p><strong>Date</strong>: event</p></li></ul>",
      `<p><a class="button" href="${site}/news">Read more on our website</a></p>`,
      "<p>With warm regards,<br>The Head of School</p>",
    ].join(""),
  },
  {
    id: "event",
    label: "Event invitation",
    title: "You're invited",
    preheader: "Join us at Maydel Spring Schools",
    html: [
      "<h2>Event name</h2>",
      "<p><strong>When:</strong> Day, date, time<br><strong>Where:</strong> Maydel Spring Schools, Abuja</p>",
      "<p>Tell families what's happening, who it's for and anything to bring.</p>",
      `<p><a class="button" href="${site}/contact">Let us know you're coming</a></p>`,
    ].join(""),
  },
  {
    id: "announcement",
    label: "Announcement",
    title: "An announcement from the school",
    preheader: "",
    html: ["<h2>Heading</h2>", "<p>Write the announcement here.</p>", `<p><a class="button" href="${site}/">Find out more</a></p>`].join(""),
  },
];

/** "2026-11-02T07:30" (WAT datetime-local) → an instant the API accepts. */
export function scheduleAt(local: string): string | null {
  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(local) ? `${local}:00+01:00` : null;
}
