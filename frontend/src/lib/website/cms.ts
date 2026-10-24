import type { components } from "@/lib/api/schema";

/** E7 website console (admin): the CMS behind the public site. */
export type ContactMessage = components["schemas"]["ContactMessageDto"];
export type MessageStatus = components["schemas"]["MessageStatus"];
export type Testimonial = components["schemas"]["TestimonialDto"];
export type TestimonialInput = components["schemas"]["TestimonialInput"];
export type Subscriber = components["schemas"]["SubscriberDto"];

export const messageStatuses: readonly MessageStatus[] = ["New", "Read", "Replied", "Archived"];
export const statusTone: Record<MessageStatus, "brand" | "neutral" | "success" | "warning"> = { New: "brand", Read: "neutral", Replied: "success", Archived: "neutral" };

/** `?status=` → a known status, or undefined for all. */
export function parseStatus(v: string | undefined): MessageStatus | undefined {
  return messageStatuses.find((s) => s === v);
}

/** A reply that opens in the admin's email app, quoting the subject. */
export function replyHref(m: Pick<ContactMessage, "email" | "subject">): string {
  const subject = m.subject ? `Re: ${m.subject}` : "Your message to Maydel Spring Schools";
  return `mailto:${encodeURIComponent(m.email)}?subject=${encodeURIComponent(subject)}`;
}

/** Testimonial form rules, mirroring the server's. */
export function testimonialErrors(t: { quote: string; attribution: string; order: string }) {
  return {
    quote: t.quote.trim() === "" ? "Enter the quote." : t.quote.length > 1000 ? "1000 characters at most." : undefined,
    attribution: t.attribution.trim() === "" ? "Say who said it, e.g. Parent of a Year 3 pupil." : t.attribution.length > 120 ? "120 characters at most." : undefined,
    order: /^\d{1,3}$/.test(t.order) ? undefined : "A whole number, 0 to 999.",
  };
}
