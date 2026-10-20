"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { ChevronLeft, ChevronRight, Pause, Play } from "lucide-react";
import { cn } from "@/components/ui/primitives";

export type CarouselSlide = { id: string; title: string; caption: string | null; image: string; href: string | null };

const INTERVAL = 6000;
const reducedQuery = "(prefers-reduced-motion: reduce)";
const subscribeReduced = (cb: () => void) => {
  const m = window.matchMedia(reducedQuery);
  m.addEventListener("change", cb);
  return () => m.removeEventListener("change", cb);
};

/**
 * E7.9 homepage carousel: advances every ~6 s right-to-left; pauses on hover, focus or touch, and with
 * the pause button; swipe and arrow keys move it; dots jump. No autoplay under prefers-reduced-motion.
 * A fixed aspect ratio keeps the layout from shifting.
 */
export function HighlightsCarousel({ slides }: { slides: CarouselSlide[] }) {
  const [index, setIndex] = useState(0);
  const [hovered, setHovered] = useState(false);
  const [focused, setFocused] = useState(false);
  const [paused, setPaused] = useState(false);
  const reduced = useSyncExternalStore(subscribeReduced, () => window.matchMedia(reducedQuery).matches, () => true);
  const root = useRef<HTMLElement>(null);
  const count = slides.length;
  const go = (i: number) => setIndex(((i % count) + count) % count);
  const playing = count > 1 && !reduced && !paused && !hovered && !focused;

  // Hover, focus, keys and swipe on the whole carousel (wired here: the region itself isn't a control).
  useEffect(() => {
    const el = root.current;
    if (!el || count < 2) return;
    let touchX: number | null = null;
    const move = (d: number) => setIndex((i) => (((i + d) % count) + count) % count);
    const on: [string, EventListener][] = [
      ["mouseenter", () => setHovered(true)],
      ["mouseleave", () => setHovered(false)],
      ["focusin", () => setFocused(true)],
      ["focusout", (e) => !el.contains((e as FocusEvent).relatedTarget as Node | null) && setFocused(false)],
      ["keydown", (e) => ((e as KeyboardEvent).key === "ArrowRight" ? move(1) : (e as KeyboardEvent).key === "ArrowLeft" ? move(-1) : undefined)],
      [
        "touchstart",
        (e) => {
          touchX = (e as TouchEvent).touches[0]?.clientX ?? null;
          setPaused(true);
        },
      ],
      [
        "touchend",
        (e) => {
          const end = (e as TouchEvent).changedTouches[0]?.clientX;
          if (touchX !== null && end !== undefined && Math.abs(end - touchX) > 40) move(end < touchX ? 1 : -1);
          touchX = null;
        },
      ],
    ];
    for (const [type, fn] of on) el.addEventListener(type, fn, { passive: true });
    return () => {
      for (const [type, fn] of on) el.removeEventListener(type, fn);
    };
  }, [count]);

  useEffect(() => {
    if (!playing) return;
    const id = setInterval(() => setIndex((i) => (i + 1) % count), INTERVAL);
    return () => clearInterval(id);
  }, [playing, count]);

  if (count === 0) return null;
  const current = slides[Math.min(index, count - 1)];

  return (
    <section ref={root} aria-roledescription="carousel" aria-label="School highlights" className="relative overflow-hidden rounded-card bg-surface-2">
      <div className="relative aspect-[16/9] w-full sm:aspect-[21/9]">
        {slides.map((s, i) => (
          <div
            key={s.id}
            role="group"
            aria-roledescription="slide"
            aria-label={`${i + 1} of ${count}: ${s.title}`}
            aria-hidden={i !== index}
            className={cn("absolute inset-0 transition-opacity duration-700 motion-reduce:transition-none", i === index ? "opacity-100" : "pointer-events-none opacity-0")}
          >
            <Image src={s.image} alt="" fill priority={i === 0} sizes="(min-width: 1152px) 1152px, 100vw" className="object-cover" />
            <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-black/20 to-transparent" />
            <div className="absolute inset-x-0 bottom-0 p-5 text-white sm:p-8">
              <p className="font-display text-xl font-semibold sm:text-3xl">{s.title}</p>
              {s.caption && <p className="mt-1 max-w-2xl text-sm sm:text-base">{s.caption}</p>}
              {s.href && (
                <Link href={s.href} tabIndex={i === index ? 0 : -1} className="mt-3 inline-flex min-h-10 items-center rounded-input bg-white/95 px-4 text-sm font-medium text-black hover:bg-white">
                  Read more
                </Link>
              )}
            </div>
          </div>
        ))}
      </div>
      <p className="sr-only" aria-live={playing ? "off" : "polite"}>
        Slide {index + 1} of {count}: {current.title}
      </p>
      {count > 1 && (
        <div className="absolute top-3 right-3 flex gap-1.5">
          {!reduced && (
            <button type="button" onClick={() => setPaused((p) => !p)} className="inline-flex size-10 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70" aria-label={paused ? "Play slideshow" : "Pause slideshow"}>
              {paused ? <Play className="size-4" aria-hidden /> : <Pause className="size-4" aria-hidden />}
            </button>
          )}
          <button type="button" onClick={() => go(index - 1)} className="inline-flex size-10 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70" aria-label="Previous slide">
            <ChevronLeft className="size-5" aria-hidden />
          </button>
          <button type="button" onClick={() => go(index + 1)} className="inline-flex size-10 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70" aria-label="Next slide">
            <ChevronRight className="size-5" aria-hidden />
          </button>
        </div>
      )}
      {count > 1 && (
        <div className="absolute inset-x-0 bottom-2 flex justify-center gap-1.5 sm:bottom-3">
          {slides.map((s, i) => (
            <button
              key={s.id}
              type="button"
              onClick={() => go(i)}
              aria-label={`Go to slide ${i + 1}`}
              aria-current={i === index ? "true" : undefined}
              className={cn("h-2 rounded-full transition-all", i === index ? "w-6 bg-white" : "w-2 bg-white/60 hover:bg-white/80")}
            />
          ))}
        </div>
      )}
    </section>
  );
}
