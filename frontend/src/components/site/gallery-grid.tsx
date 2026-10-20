"use client";

import Image from "next/image";
import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { FormDialog } from "@/components/ui/dialog";

export type GalleryItem = { id: string; title: string; caption: string | null; category: string; image: string };

/** E7: photo grid with a lightbox (arrow keys and buttons move between photos; Esc closes). */
export function GalleryGrid({ photos }: { photos: GalleryItem[] }) {
  const [open, setOpen] = useState<number | null>(null);
  const current = open === null ? null : photos[open];
  const step = (d: number) => setOpen((i) => (i === null ? i : (i + d + photos.length) % photos.length));
  const isOpen = open !== null;
  useEffect(() => {
    if (!isOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "ArrowRight") setOpen((i) => (i === null ? i : (i + 1) % photos.length));
      if (e.key === "ArrowLeft") setOpen((i) => (i === null ? i : (i - 1 + photos.length) % photos.length));
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [isOpen, photos.length]);

  return (
    <>
      <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
        {photos.map((p, i) => (
          <li key={p.id}>
            <button type="button" onClick={() => setOpen(i)} className="group relative block aspect-square w-full overflow-hidden rounded-card bg-surface-2" aria-label={`Open photo: ${p.title}`}>
              <Image src={p.image} alt="" fill sizes="(min-width: 1024px) 280px, (min-width: 640px) 33vw, 50vw" className="object-cover transition-transform duration-300 group-hover:scale-105 motion-reduce:transition-none" />
            </button>
          </li>
        ))}
      </ul>
      {current && (
        <FormDialog title={current.title} description={current.caption ?? undefined} size="lg" open onOpenChange={(v) => !v && setOpen(null)}>
          {() => (
            <div className="space-y-3">
              <div className="relative aspect-[4/3] w-full overflow-hidden rounded-input bg-black">
                <Image src={current.image} alt={current.caption ?? current.title} fill sizes="(min-width: 768px) 720px, 100vw" className="object-contain" />
              </div>
              {photos.length > 1 && (
                <div className="flex items-center justify-between text-sm">
                  <button type="button" onClick={() => step(-1)} className="inline-flex min-h-10 items-center gap-1 rounded-input px-3 hover:bg-surface-2">
                    <ChevronLeft className="size-4" aria-hidden />
                    Previous
                  </button>
                  <span className="tabular text-muted">
                    {(open ?? 0) + 1} of {photos.length}
                  </span>
                  <button type="button" onClick={() => step(1)} className="inline-flex min-h-10 items-center gap-1 rounded-input px-3 hover:bg-surface-2">
                    Next
                    <ChevronRight className="size-4" aria-hidden />
                  </button>
                </div>
              )}
            </div>
          )}
        </FormDialog>
      )}
    </>
  );
}
