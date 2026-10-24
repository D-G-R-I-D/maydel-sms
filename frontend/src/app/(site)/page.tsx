import Image from "next/image";
import Link from "next/link";
import { Award, ArrowRight, BookOpen, GraduationCap, HeartHandshake, Quote, Users } from "lucide-react";
import { loadSite, siteImage } from "@/lib/site/api";
import { anniversary, hero, highlights as highlightChips, pillars, programmes, siteImages, story, testimonials } from "@/lib/site/content";
import { CrestWatermark, DriftingCircles } from "@/components/ui/drifting-circles";
import { Card } from "@/components/ui/primitives";
import { HighlightsCarousel, type CarouselSlide } from "@/components/site/highlights-carousel";
import { NewsTicker } from "@/components/site/news-ticker";
import { HeroBackdrop } from "@/components/site/hero-backdrop";
import { EventItem, NewsCard, SectionHeading } from "@/components/site/cards";

const pillarIcons = [GraduationCap, HeartHandshake, Users, BookOpen];

/** E7.9: the homepage. Highlights carousel and ticker from the CMS, the school's story, latest news and events. */
export default async function HomePage() {
  const [highlights, news, events, admissions] = await Promise.all([
    loadSite((api) => api.GET("/api/public/site/highlights")),
    loadSite((api) => api.GET("/api/public/site/news", { params: { query: { page: 1, pageSize: 3 } } })),
    loadSite((api) => api.GET("/api/public/site/events", { params: { query: { take: 3 } } })),
    loadSite((api) => api.GET("/api/public/admissions")),
  ]);
  const open = admissions.ok ? admissions.data.find((s) => s.isOpen) : undefined;
  const slides: CarouselSlide[] = highlights.ok
    ? highlights.data.slides.flatMap((s) => {
        const image = siteImage(s.imageUrl);
        return image ? [{ id: `${s.kind}-${s.id}`, title: s.title, caption: s.caption, image, href: s.href }] : [];
      })
    : [];

  return (
    <>
      {highlights.ok && <NewsTicker items={highlights.data.ticker} />}

      {/* Full-bleed hero: the campus, the seniors and the little ones in turn, a navy wash for legible text, and the drifting circles over it. */}
      <section className="relative isolate overflow-hidden bg-navy text-white" aria-labelledby="hero-title">
        <HeroBackdrop
          photos={[
            { wide: siteImages.hero.src, photo: siteImages.heroPhone.src, position: "object-[50%_40%]" },
            { wide: siteImages.heroSeniors.src, photo: siteImages.seniors.src, position: "object-[88%_30%]" },
            { wide: siteImages.heroNursery.src, photo: siteImages.nurseryCrest.src, position: "object-[88%_40%]" },
          ]}
        />
        <div className="absolute inset-0 -z-10 hidden bg-gradient-to-r from-[#0f2a44]/90 via-[#0f2a44]/55 to-[#0f2a44]/10 sm:block" aria-hidden />
        <div className="absolute inset-x-0 bottom-0 -z-10 hidden h-32 bg-gradient-to-t from-[#0f2a44]/70 to-transparent sm:block" aria-hidden />
        <DriftingCircles density="light" />
        <div className="relative z-10 mx-auto flex min-h-[calc(100svh-4rem)] max-w-6xl flex-col justify-end gap-5 px-4 pt-[60vw] pb-10 sm:min-h-[32rem] sm:justify-center sm:px-6 sm:py-16 lg:min-h-[36rem]">
          <p className="text-sm font-semibold tracking-[0.18em] text-gold uppercase">Abuja · Since {story.founded}</p>
          <h1 id="hero-title" className="max-w-2xl font-display text-4xl leading-tight font-semibold text-balance sm:text-6xl">
            {hero.titleLead} <em className="text-gold">{hero.titleAccent}</em>
          </h1>
          <p className="max-w-xl text-lg text-white/85">{hero.text}</p>
          <div className="flex flex-wrap gap-3">
            <Link href={open ? "/admissions/apply" : "/admissions-info"} className="inline-flex min-h-12 items-center gap-2 rounded-input bg-gold px-6 font-semibold text-[#0f2a44] hover:bg-gold/90">
              {open ? "Apply for admission" : "How admission works"}
              <ArrowRight className="size-4" aria-hidden />
            </Link>
            <Link href="/about" className="inline-flex min-h-12 items-center rounded-input border border-white/40 px-6 font-medium text-white hover:bg-white/10">
              About the school
            </Link>
          </div>
          <p className="flex items-center gap-2 text-sm text-white/80">
            <Award className="size-4 text-gold" aria-hidden />
            An approved WAEC and NECO examination centre
          </p>
        </div>
      </section>

      {/* Reasons to choose us, drifting past (reduced motion: a static wrapped list). */}
      <section aria-label="At a glance" className="overflow-hidden border-y border-border bg-surface py-3">
        <div className="ticker-track flex w-max gap-3 hover:[animation-play-state:paused]">
          {[false, true].map((dup) => (
            <ul key={String(dup)} className="flex shrink-0 gap-3 pr-3" aria-hidden={dup || undefined}>
              {highlightChips.map((h) => (
                <li key={h} className="inline-flex items-center gap-2 rounded-full border border-border px-4 py-1.5 text-sm whitespace-nowrap">
                  <span className="size-1.5 rounded-full bg-gold" aria-hidden />
                  {h}
                </li>
              ))}
            </ul>
          ))}
        </div>
      </section>

      {slides.length > 0 && (
        <section className="mx-auto max-w-6xl px-4 pt-10 sm:px-6" aria-label="Highlights">
          <HighlightsCarousel slides={slides} />
        </section>
      )}

      <section className="mx-auto max-w-6xl px-4 py-10 sm:px-6" aria-label="By the numbers">
        <dl className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          {[
            [String(story.founded), "Shaping bright futures since"],
            ["5", "School levels, from Creche to Senior Secondary"],
            ["WAEC · NECO", "Approved examination centre"],
            [`${anniversary.years} years`, `Celebrating our anniversary in ${anniversary.when}`],
          ].map(([value, label]) => (
            <div key={label} className="rounded-card border border-border bg-surface p-5">
              <dt className="text-sm text-muted">{label}</dt>
              <dd className="mt-1 font-display text-3xl font-semibold text-brand">{value}</dd>
            </div>
          ))}
        </dl>
      </section>

      <section className="relative overflow-hidden bg-surface py-12" aria-labelledby="why">
        <CrestWatermark side="right" />
        <div className="relative z-10 mx-auto max-w-6xl px-4 sm:px-6">
          <SectionHeading eyebrow="Why families choose us" title="An education built on foundations that last." />
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4" id="why">
            {pillars.map((p, i) => {
              const Icon = pillarIcons[i];
              return (
                <li key={p.title}>
                  <Card className="h-full space-y-2 p-5">
                    <Icon className="size-6 text-brand" aria-hidden />
                    <h3 className="font-semibold">{p.title}</h3>
                    <p className="text-sm text-muted">{p.text}</p>
                  </Card>
                </li>
              );
            })}
          </ul>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-12 sm:px-6" aria-label="Programmes">
        <SectionHeading eyebrow="Our programmes" title="Every stage of your child's learning journey." />
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {programmes.map((p) => (
            <li key={p.title}>
              <Card className="h-full space-y-1 p-5">
                <p className="text-xs font-semibold text-brand">{p.range}</p>
                <h3 className="font-display text-lg font-semibold">{p.title}</h3>
                <p className="text-sm text-muted">{p.text}</p>
              </Card>
            </li>
          ))}
        </ul>
        <Link href="/admissions-info" className="mt-5 inline-flex items-center gap-1 font-medium text-brand hover:underline">
          How admission works
          <ArrowRight className="size-4" aria-hidden />
        </Link>
      </section>

      {news.ok && news.data.items.length > 0 && (
        <section className="bg-surface py-12" aria-label="Latest news">
          <div className="mx-auto max-w-6xl px-4 sm:px-6">
            <SectionHeading eyebrow="News and announcements" title="What's happening at Maydel." />
            <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {news.data.items.map((n) => (
                <li key={n.id}>
                  <NewsCard item={n} />
                </li>
              ))}
            </ul>
            <Link href="/news" className="mt-5 inline-flex items-center gap-1 font-medium text-brand hover:underline">
              All news
              <ArrowRight className="size-4" aria-hidden />
            </Link>
          </div>
        </section>
      )}

      <section className="mx-auto max-w-6xl px-4 py-12 sm:px-6" aria-label="Life at Maydel">
        <SectionHeading eyebrow="Life at Maydel" title="Bright, safe spaces to learn and play.">
          A fenced, gated campus with CCTV, airy classrooms, a shaded early-years play area, courts and a football pitch.
        </SectionHeading>
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4 lg:grid-rows-2">
          {[siteImages.nurseryCrest, siteImages.aerobics, siteImages.coveredPlay, siteImages.culture, siteImages.courtyard].map((img, i) => (
            <div key={img.src} className={`relative overflow-hidden rounded-card ${i === 0 ? "col-span-2 row-span-2 aspect-[4/3] lg:aspect-auto" : "aspect-[4/3]"}`}>
              <Image src={img.src} alt={img.alt} fill sizes={i === 0 ? "(min-width: 1024px) 560px, 100vw" : "(min-width: 1024px) 280px, 50vw"} className="object-cover transition-transform duration-500 hover:scale-105 motion-reduce:transition-none" />
            </div>
          ))}
        </div>
        <Link href="/gallery" className="mt-5 inline-flex items-center gap-1 font-medium text-brand hover:underline">
          See the gallery
          <ArrowRight className="size-4" aria-hidden />
        </Link>
      </section>

      <section className="relative overflow-hidden bg-surface py-12" aria-labelledby="voices">
        <CrestWatermark side="left" />
        <div className="relative z-10 mx-auto max-w-6xl px-4 sm:px-6">
          <SectionHeading eyebrow="From our community" title="Voices from the Maydel family." />
          <ul className="grid gap-4 lg:grid-cols-3" id="voices">
            {testimonials.map((t) => (
              <li key={t.by}>
                <figure className="flex h-full flex-col rounded-card border border-border bg-bg p-6">
                  <Quote className="size-6 text-gold" aria-hidden />
                  <blockquote className="mt-3 flex-1 text-base">{t.quote}</blockquote>
                  <figcaption className="mt-4 text-sm">
                    <span className="font-medium">{t.by}</span>
                    <span className="block text-muted">{t.section}</span>
                  </figcaption>
                </figure>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 pt-12 sm:px-6" aria-labelledby="recognition">
        <div className="flex flex-col gap-5 rounded-card border border-gold/40 bg-gold/5 p-6 sm:flex-row sm:items-center sm:p-8">
          <span className="inline-flex size-14 shrink-0 items-center justify-center rounded-full bg-gold/15 text-gold">
            <Award className="size-7" aria-hidden />
          </span>
          <div>
            <h2 id="recognition" className="font-display text-xl font-semibold">
              Approved WAEC and NECO examination centre
            </h2>
            <p className="mt-1 text-muted">
              Maydel Spring Schools is approved to register and host candidates for both the West African Examinations Council and the National Examinations Council, so our students sit their
              national examinations right here on campus.
            </p>
          </div>
        </div>
      </section>

      <section className="mx-auto grid max-w-6xl gap-10 px-4 py-12 sm:px-6 lg:grid-cols-2">
        <div>
          <SectionHeading eyebrow="Our story" title="Every child deserves a strong start." />
          <figure className="flex gap-4">
            <Image src={siteImages.founder.src} alt={siteImages.founder.alt} width={96} height={96} className="size-24 shrink-0 rounded-full object-cover" />
            <blockquote>
              <p className="font-display text-lg italic">&ldquo;{story.founderQuote}&rdquo;</p>
              <figcaption className="mt-2 text-sm text-muted">
                {story.founder}, {story.founderRole}
              </figcaption>
            </blockquote>
          </figure>
          <p className="mt-5 text-muted">{story.paragraphs[0]}</p>
          <Link href="/about" className="mt-3 inline-flex items-center gap-1 font-medium text-brand hover:underline">
            Read our story
            <ArrowRight className="size-4" aria-hidden />
          </Link>
        </div>
        <div>
          <SectionHeading eyebrow="Coming up" title="Upcoming events" />
          {events.ok && events.data.length > 0 ? (
            <ul className="space-y-5">
              {events.data.map((e) => (
                <li key={e.id}>
                  <EventItem event={e} />
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted">No upcoming events right now. Check back soon.</p>
          )}
          <Link href="/events" className="mt-5 inline-flex items-center gap-1 font-medium text-brand hover:underline">
            All events
            <ArrowRight className="size-4" aria-hidden />
          </Link>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 sm:px-6">
        <div className="brand-panel relative flex flex-col items-start gap-4 overflow-hidden rounded-card p-8 text-white sm:flex-row sm:items-center">
          <DriftingCircles density="light" />
          <div className="relative z-10 flex-1">
            <h2 className="font-display text-2xl font-semibold">{open ? `Admissions for ${open.session} are open.` : "Thinking about joining us?"}</h2>
            <p className="mt-1 opacity-90">
              {open ? "Buy the admission form online and track your child's application from your family account." : "Admissions aren't open right now. Contact the school office to find out about the next intake."}
            </p>
          </div>
          <Link href={open ? "/admissions/apply" : "/contact"} className="relative z-10 inline-flex min-h-11 items-center gap-2 rounded-input bg-white px-5 font-medium text-brand hover:bg-white/90">
            {open ? "Start an application" : "Contact us"}
            <ArrowRight className="size-4" aria-hidden />
          </Link>
        </div>
      </section>
    </>
  );
}
