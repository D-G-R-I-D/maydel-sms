"use client";

import { useEffect, useState } from "react";
import Image from "next/image";

/** `wide` fills the hero from `sm` up; `photo` is the plain 4:3 picture used on phones. */
export type HeroPhoto = { wide: string; photo: string; position: string };

/**
 * The homepage hero's photos, cross-fading every few seconds. On phones the whole 4:3 photo sits at the
 * top, so every face shows, and melts into a blurred, tinted copy of itself that fills the rest of the
 * screen behind the headline. From `sm` up the wide version fills the hero. Decorative (the headline
 * carries the meaning), so the images have empty alt text. With reduced motion it stays on the first.
 */
export function HeroBackdrop({ photos, interval = 7000 }: { photos: readonly HeroPhoto[]; interval?: number }) {
  const [current, setCurrent] = useState(0);

  useEffect(() => {
    if (photos.length < 2 || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const id = window.setInterval(() => setCurrent((c) => (c + 1) % photos.length), interval);
    return () => window.clearInterval(id);
  }, [photos.length, interval]);

  const fade = (i: number) => `transition-opacity duration-[1500ms] ease-in-out ${i === current ? "opacity-100" : "opacity-0"}`;

  return (
    <div className="absolute inset-0 -z-20" aria-hidden>
      {photos.map((p, i) => (
        <div key={`m-${p.photo}`} className={`absolute inset-0 sm:hidden ${fade(i)}`}>
          <Image src={p.photo} alt="" fill sizes="50vw" className="scale-125 object-cover blur-2xl brightness-[0.45]" />
          <div className="absolute inset-x-0 top-0 aspect-[4/3] [mask-image:linear-gradient(to_bottom,black_72%,transparent)]">
            <Image src={p.photo} alt="" fill priority={i === 0} sizes="100vw" className="object-cover" />
          </div>
        </div>
      ))}
      <div className="absolute inset-0 bg-gradient-to-b from-transparent via-[#0f2a44]/40 to-[#0f2a44]/80 sm:hidden" />
      {photos.map((p, i) => (
        <Image key={`w-${p.wide}`} src={p.wide} alt="" fill priority={i === 0} sizes="100vw" className={`hidden object-cover sm:block ${p.position} ${fade(i)}`} />
      ))}
    </div>
  );
}
