import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ArrowRight, Download } from "lucide-react";
import { loadSite } from "@/lib/site/api";
import { admissionSteps, faqs, programmes, siteImages } from "@/lib/site/content";
import { formatNaira } from "@/lib/finance/rules";
import { Card, buttonClasses } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

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
          <Alert tone="info" title="Admissions aren't open right now">
            Please check back later, or <Link href="/contact" className="font-medium text-brand hover:underline">contact the school office</Link> about the next intake.
          </Alert>
        )}
      </header>
        <div className="relative hidden aspect-[3/4] overflow-hidden rounded-card lg:block">
          <Image src={siteImages.signboard.src} alt={siteImages.signboard.alt} fill priority sizes="(min-width: 1024px) 420px, 0px" className="object-cover object-top" />
        </div>
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
        {open.length > 0 && (
          <a href={`/api/media/admissions/blank-form?sessionId=${open[0].sessionId}`} className="mt-4 inline-flex items-center gap-1.5 text-sm font-medium text-brand hover:underline">
            <Download className="size-4" aria-hidden />
            Prefer paper? Download a blank admission form (PDF)
          </a>
        )}
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
