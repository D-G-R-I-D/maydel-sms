import type { Metadata } from "next";
import Image from "next/image";
import { siteImages, story } from "@/lib/site/content";
import { Card } from "@/components/ui/primitives";

export const metadata: Metadata = {
  title: "About us",
  description: "Our story, vision, mission and values: Maydel Spring Schools, Abuja, founded in 2022 by Proprietress Lekwot Chiamaka.",
};

/** E7: the school's story, vision, mission and values (carried over from the previous site). */
export default function AboutPage() {
  return (
    <div className="mx-auto max-w-6xl space-y-12 px-4 py-10 sm:px-6">
      <header className="max-w-3xl">
        <p className="text-sm font-semibold tracking-wide text-brand uppercase">About Maydel Spring Schools</p>
        <h1 className="mt-1 font-display text-4xl font-semibold text-balance">Founded on a simple belief: every child deserves a strong start.</h1>
      </header>

      <section className="grid gap-8 lg:grid-cols-[2fr_1fr] lg:items-start" aria-labelledby="story">
        <div className="space-y-4">
          <h2 id="story" className="font-display text-2xl font-semibold">
            Our story
          </h2>
          {story.paragraphs.map((p) => (
            <p key={p.slice(0, 20)} className="text-muted">
              {p}
            </p>
          ))}
        </div>
        <figure className="space-y-3">
          <div className="relative aspect-[4/5] overflow-hidden rounded-card">
            <Image src={siteImages.founder.src} alt={siteImages.founder.alt} fill sizes="(min-width: 1024px) 360px, 100vw" className="object-cover" />
          </div>
          <figcaption>
            <p className="font-display text-lg italic">&ldquo;{story.founderQuote}&rdquo;</p>
            <p className="mt-1 text-sm text-muted">
              {story.founder}, {story.founderRole}
            </p>
          </figcaption>
        </figure>
      </section>

      <section className="grid gap-4 sm:grid-cols-2" aria-label="Vision and mission">
        <Card className="space-y-2 p-6">
          <h2 className="font-display text-xl font-semibold">Our vision</h2>
          <p className="text-muted">{story.vision}</p>
        </Card>
        <Card className="space-y-2 p-6">
          <h2 className="font-display text-xl font-semibold">Our mission</h2>
          <p className="text-muted">{story.mission}</p>
        </Card>
      </section>

      <section aria-labelledby="values">
        <h2 id="values" className="font-display text-2xl font-semibold">
          Core values
        </h2>
        <p className="mt-1 text-muted">The principles that shape our community and our approach.</p>
        <ul className="mt-4 flex flex-wrap gap-2">
          {story.values.map((v) => (
            <li key={v} className="rounded-full border border-brand/30 bg-brand/5 px-4 py-2 font-medium text-brand">
              {v}
            </li>
          ))}
        </ul>
      </section>

      <section className="grid gap-4 sm:grid-cols-2" aria-label="School life">
        {[siteImages.culture, siteImages.courtyard].map((img) => (
          <div key={img.src} className="relative aspect-[4/3] overflow-hidden rounded-card">
            <Image src={img.src} alt={img.alt} fill sizes="(min-width: 640px) 50vw, 100vw" className="object-cover" />
          </div>
        ))}
      </section>
    </div>
  );
}
