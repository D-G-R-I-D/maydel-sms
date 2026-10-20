import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ArrowRight, Bus, Clock, Music, ShieldCheck, Shirt, ScrollText, BookOpen, Utensils } from "lucide-react";
import { parentFaqs, parentGuide, siteImages } from "@/lib/site/content";
import { Card, buttonClasses } from "@/components/ui/primitives";

export const metadata: Metadata = {
  title: "For parents",
  description: "School hours, the school bus, security, uniform, meals, clubs, curriculum and policies at Maydel Spring Schools, Abuja.",
};

const icons = { hours: Clock, transport: Bus, security: ShieldCheck, uniform: Shirt, meals: Utensils, clubs: Music, curriculum: BookOpen, policies: ScrollText } as const;

/** E7: everything a Maydel parent wants to know (the previous site's "For Parents" guide). */
export default function ParentsPage() {
  return (
    <div className="mx-auto max-w-6xl space-y-12 px-4 py-10 sm:px-6">
      <header className="grid gap-6 lg:grid-cols-2 lg:items-center">
        <div className="space-y-3">
          <p className="text-sm font-semibold tracking-wide text-brand uppercase">For parents</p>
          <h1 className="font-display text-4xl font-semibold text-balance">Everything you need to know.</h1>
          <p className="text-lg text-muted">School hours, the bus, security, uniform, meals and more, plus the parent portal for results, fees and attendance.</p>
          <nav aria-label="On this page" className="flex flex-wrap gap-2 pt-1">
            {parentGuide.map((g) => (
              <a key={g.id} href={`#${g.id}`} className="inline-flex min-h-9 items-center rounded-full border border-border px-3 text-sm hover:bg-surface-2">
                {g.title}
              </a>
            ))}
          </nav>
        </div>
        <div className="relative aspect-[4/3] overflow-hidden rounded-card">
          <Image src={siteImages.playArea.src} alt={siteImages.playArea.alt} fill priority sizes="(min-width: 1024px) 560px, 100vw" className="object-cover" />
        </div>
      </header>

      <section aria-label="Parent guide">
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {parentGuide.map((g) => {
            const Icon = icons[g.id];
            return (
              <li key={g.id} id={g.id} className="scroll-mt-24">
                <Card className="h-full space-y-3 p-5">
                  <span className="inline-flex size-10 items-center justify-center rounded-full bg-brand/10 text-brand">
                    <Icon className="size-5" aria-hidden />
                  </span>
                  <h2 className="font-display text-lg font-semibold">{g.title}</h2>
                  <p className="text-sm text-muted">{g.text}</p>
                  <ul className="space-y-1.5 text-sm">
                    {g.points.map((pt) => (
                      <li key={pt} className="flex gap-2">
                        <span className="mt-2 size-1.5 shrink-0 rounded-full bg-gold" aria-hidden />
                        {pt}
                      </li>
                    ))}
                  </ul>
                </Card>
              </li>
            );
          })}
        </ul>
      </section>

      <section className="grid gap-6 rounded-card bg-surface p-6 sm:p-8 lg:grid-cols-[2fr_1fr] lg:items-center" aria-labelledby="portal">
        <div>
          <h2 id="portal" className="font-display text-2xl font-semibold">
            The parent portal
          </h2>
          <p className="mt-2 text-muted">Report cards the moment they&apos;re published, attendance, fees and receipts, computer-based test results and school notices, all on your phone.</p>
        </div>
        <Link href="/login" className={buttonClasses("primary", "lg")}>
          Sign in to the portal
          <ArrowRight className="size-4" aria-hidden />
        </Link>
      </section>

      <section aria-labelledby="pfaq" className="max-w-3xl">
        <h2 id="pfaq" className="font-display text-2xl font-semibold">
          Parents ask
        </h2>
        <div className="mt-4 divide-y divide-border rounded-card border border-border bg-surface">
          {parentFaqs.map((f) => (
            <details key={f.q} className="group p-4">
              <summary className="cursor-pointer list-none font-medium">
                <span className="flex items-center justify-between gap-3">
                  {f.q}
                  <span className="text-brand transition-transform group-open:rotate-45" aria-hidden>
                    +
                  </span>
                </span>
              </summary>
              <p className="mt-2 text-sm text-muted">{f.a}</p>
            </details>
          ))}
        </div>
      </section>
    </div>
  );
}
