import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { ApplicationChecklist } from "@/components/site/application-checklist";
import { loadSite } from "@/lib/site/api";
import { admissionSteps, faqs, programmes, siteImages } from "@/lib/site/content";
import { DriftingCircles } from "@/components/ui/drifting-circles";
import { formatNaira } from "@/lib/finance/rules";
import { Card, buttonClasses } from "@/components/ui/primitives";

export const metadata: Metadata = {
  title: "Admissions",
  description: "How to apply to Maydel Spring Schools, Abuja: buy the admission form online, fill it in, book the entrance exam and accept your offer.",
};

/** E7: how admission works, with the live open/closed state and the form fee from the admissions settings. */
export default async function AdmissionsInfoPage() {
  const info = await loadSite((api) => api.GET("/api/public/admissions"));
  const open = info.ok ? info.data.filter((s) => s.isOpen) : [];

  return (
    <div className="mx-auto max-w-6xl space-y-12 px-4 py-10 sm:px-6">
      <div className="grid gap-8 lg:grid-cols-[3fr_2fr] lg:items-center">
      <header className="max-w-3xl space-y-3">
        <p className="text-sm font-semibold tracking-wide text-brand uppercase">Admissions</p>
        <h1 className="font-display text-4xl font-semibold text-balance">Give your child a strong start.</h1>
        <p className="text-lg text-muted">Our admissions process is simple and supportive, and you can do most of it online from a family account.</p>
        {open.length > 0 ? (
          <div className="flex flex-wrap items-center gap-3 pt-2">
            <Link href="/admissions/apply" className={buttonClasses("primary", "lg")}>
              Buy the admission form
              <ArrowRight className="size-4" aria-hidden />
            </Link>
            {open.map((s) => (
              <span key={s.sessionId} className="text-sm text-muted">
                {s.session}: {s.formFee > 0 ? `form fee ${formatNaira(s.formFee)}` : "no form fee"}
              </span>
            ))}
          </div>
        ) : (
          <p className="text-muted">
            Admissions aren&apos;t open right now. Check back soon, or{" "}
            <Link href="/contact" className="font-medium text-brand hover:underline">
              ask the school office
            </Link>{" "}
            about the next intake.
          </p>
        )}
      </header>
        {/* The nursery classroom over a branded panel: the live admissions status at a glance. */}
        <aside className="brand-panel relative overflow-hidden rounded-card text-white" aria-label="Admissions at a glance">
          <div className="relative aspect-[16/9]">
            <Image src={siteImages.nursery.src} alt={siteImages.nursery.alt} fill sizes="(min-width: 1024px) 400px, 100vw" className="object-cover object-[50%_60%]" />
            <div className="absolute inset-x-0 bottom-0 h-1/3 bg-gradient-to-t from-[#183754] to-transparent" aria-hidden />
          </div>
          <DriftingCircles density="light" />
          <div className="relative z-10 space-y-5 p-8 pt-2">
            <span className="inline-flex size-16 items-center justify-center rounded-full bg-white/95 p-2.5 shadow-lg">
              <Image src="/brand/crest.svg" alt="" width={40} height={50} className="h-auto w-10" />
            </span>
            <div>
              <p className="text-sm font-semibold tracking-[0.18em] text-gold uppercase">{open.length > 0 ? "Now admitting" : "Admissions"}</p>
              <p className="mt-1 font-display text-3xl font-semibold">{open.length > 0 ? `${open[0].session} session` : "Opening soon"}</p>
            </div>
            <ul className="space-y-2 text-sm text-white/90">
              <li className="flex gap-2">
                <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-gold" aria-hidden />
                Creche and Nursery through Senior Secondary
              </li>
              <li className="flex gap-2">
                <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-gold" aria-hidden />
                {open.length > 0 ? (open[0].formFee > 0 ? `Form fee ${formatNaira(open[0].formFee)}, paid online or at school` : "No form fee this session") : "Ask the office about the next intake"}
              </li>
              <li className="flex gap-2">
                <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-gold" aria-hidden />
                Apply online and track every step from your family account
              </li>
            </ul>
            <Link
              href={open.length > 0 ? "/admissions/apply" : "/contact"}
              className="inline-flex min-h-11 items-center gap-2 rounded-input bg-gold px-5 font-semibold text-[#0f2a44] hover:bg-gold/90"
            >
              {open.length > 0 ? "Apply now" : "Contact the office"}
              <ArrowRight className="size-4" aria-hidden />
            </Link>
          </div>
        </aside>
      </div>

      <section aria-labelledby="steps">
        <h2 id="steps" className="font-display text-2xl font-semibold">
          Four simple steps
        </h2>
        <ol className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {admissionSteps.map((s, i) => (
            <li key={s.title}>
              <Card className="h-full space-y-2 p-5">
                <span className="inline-flex size-8 items-center justify-center rounded-full bg-brand text-sm font-semibold text-on-brand" aria-hidden>
                  {i + 1}
                </span>
                <h3 className="font-semibold">{s.title}</h3>
                <p className="text-sm text-muted">{s.text}</p>
              </Card>
            </li>
          ))}
        </ol>
        <ApplicationChecklist className="mt-4" />
      </section>

      <section aria-labelledby="levels">
        <h2 id="levels" className="font-display text-2xl font-semibold">
          Classes we admit into
        </h2>
        <ul className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {programmes.map((p) => (
            <li key={p.title}>
              <Card className="h-full space-y-1 p-5">
                <p className="text-xs font-semibold text-brand">{p.range}</p>
                <h3 className="font-semibold">{p.title}</h3>
                <p className="text-sm text-muted">{p.text}</p>
              </Card>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="faq" className="max-w-3xl">
        <h2 id="faq" className="font-display text-2xl font-semibold">
          Questions and answers
        </h2>
        <div className="mt-4 divide-y divide-border rounded-card border border-border bg-surface">
          {faqs.map((f) => (
            <details key={f.q} className="group p-4">
              <summary className="cursor-pointer list-none font-medium marker:hidden">
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
