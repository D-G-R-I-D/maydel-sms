import type { Metadata } from "next";
import { NewsletterLinkAction } from "@/components/site/newsletter-link-action";

export const metadata: Metadata = { title: "Confirm your subscription", robots: { index: false, follow: false }, referrer: "no-referrer" };

/** E12.6: from the newsletter email link; acts only when the button is pressed. */
export default async function Page({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const raw = (await searchParams).t;
  const token = (Array.isArray(raw) ? raw[0] : raw) ?? "";
  return (
    <div className="mx-auto max-w-xl px-4 py-12 sm:px-6">
      <h1 className="mb-5 font-display text-3xl font-semibold">Confirm your subscription</h1>
      <NewsletterLinkAction kind="confirm" token={token} />
    </div>
  );
}
