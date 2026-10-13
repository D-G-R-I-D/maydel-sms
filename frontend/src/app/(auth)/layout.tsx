import Image from "next/image";
import { BookOpenCheck, ShieldCheck, Smartphone } from "lucide-react";

const highlights = [
  { icon: BookOpenCheck, text: "Results and report cards the moment they're published" },
  { icon: Smartphone, text: "Built for phones first, light on mobile data" },
  { icon: ShieldCheck, text: "Your family's information stays private and secure" },
];

/** Sign-in and password pages: navy brand panel beside the form; the form alone at phone width. */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
      <aside className="brand-panel relative hidden overflow-hidden p-12 text-navy-ink lg:flex lg:flex-col lg:justify-between">
        <div className="animate-fade flex items-center gap-3">
          <span className="inline-flex size-14 items-center justify-center rounded-full bg-white/95 p-2 shadow-lg">
            <Image src="/brand/crest.svg" alt="" width={34} height={42} priority />
          </span>
          <span className="font-display text-xl font-semibold">Maydel Spring Schools</span>
        </div>

        <div className="max-w-md">
          <p className="animate-rise mb-4 text-sm font-semibold uppercase tracking-[0.18em] text-gold">The Well Spring of Knowledge</p>
          <p className="animate-rise delay-1 font-display text-[clamp(2.25rem,3.4vw,3.25rem)] leading-[1.08] font-semibold">
            Every child deserves a <em className="text-gold">strong, joyful start.</em>
          </p>
          <ul className="mt-8 space-y-3">
            {highlights.map(({ icon: Icon, text }, i) => (
              <li key={text} className={`animate-rise flex items-center gap-3 text-base text-navy-ink/85 ${["delay-2", "delay-3", "delay-4"][i]}`}>
                <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-full bg-white/10 ring-1 ring-white/15">
                  <Icon className="size-4 text-gold" aria-hidden />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>

        <p className="text-sm text-navy-ink/60">Abuja, Nigeria · Creche to Senior Secondary</p>
      </aside>

      <main id="main" className="relative flex items-center justify-center px-4 py-10 sm:px-8">
        <div className="animate-rise w-full max-w-sm">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <Image src="/brand/crest.svg" alt="" width={36} height={45} priority />
            <span className="font-display text-lg leading-tight font-semibold text-brand">
              Maydel Spring Schools
              <span className="block text-xs font-normal tracking-wide text-muted">The Well Spring of Knowledge</span>
            </span>
          </div>
          {children}
        </div>
      </main>
    </div>
  );
}
