import type { Metadata } from "next";
import Link from "next/link";
import { Mail } from "lucide-react";
import { loadSite } from "@/lib/site/api";
import { formatDay } from "@/lib/site/format";
import { Card } from "@/components/ui/primitives";
import { EmptyState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Newsletters", description: "Past newsletters from Maydel Spring Schools, Abuja." };

/** E12.6: the newsletter archive (those the school chose to publish), newest first. */
export default async function NewsletterArchivePage() {
  const list = await loadSite((api) => api.GET("/api/public/newsletters"));
  const items = list.ok ? list.data : [];
  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <h1 className="font-display text-4xl font-semibold">Newsletters</h1>
      <p className="mt-1 text-muted">News from the school, as sent to our families. Sign up at the bottom of any page to get the next one.</p>
      {items.length === 0 ? (
        <Card className="mt-6">
          <EmptyState icon={Mail} title="No newsletters yet" />
        </Card>
      ) : (
        <ul className="mt-6 space-y-3">
          {items.map((n) => (
            <li key={n.slug}>
              <Link href={`/newsletters/${n.slug}`} className="block rounded-card border border-border bg-surface p-5 transition-shadow hover:shadow-low">
                <p className="text-xs text-muted">{formatDay(n.sentAt)}</p>
                <p className="mt-1 font-display text-xl font-semibold">{n.title}</p>
                {n.preheader && <p className="mt-1 text-sm text-muted">{n.preheader}</p>}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
