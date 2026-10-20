import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { Image as ImageIcon } from "lucide-react";
import { loadSite, siteImage } from "@/lib/site/api";
import { siteImages } from "@/lib/site/content";
import { GalleryGrid, type GalleryItem } from "@/components/site/gallery-grid";
import { Card, cn } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Gallery", description: "Photos of school life at Maydel Spring Schools, Abuja." };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E7: photos by album; tap one for the lightbox. */
export default async function GalleryPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const category = one((await searchParams).category)?.slice(0, 60);
  const gallery = await loadSite((api) => api.GET("/api/public/site/gallery", { params: { query: { category } } }));
  const photos: GalleryItem[] = gallery.ok
    ? gallery.data.photos.flatMap((p) => {
        const image = siteImage(p.imageUrl);
        return image ? [{ id: p.id, title: p.title, caption: p.caption, category: p.category, image }] : [];
      })
    : [];

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="font-display text-4xl font-semibold">Gallery</h1>
      <p className="mt-1 text-muted">Every day brings new moments of discovery, friendship and growth. Here&apos;s a glimpse of school life.</p>
      {!category && (
        <section aria-labelledby="featured" className="mt-6 grid overflow-hidden rounded-card border border-border bg-surface md:grid-cols-2">
          <div className="relative aspect-[4/3] md:aspect-auto">
            <Image src={siteImages.aerobics.src} alt={siteImages.aerobics.alt} fill priority sizes="(min-width: 768px) 560px, 100vw" className="object-cover" />
          </div>
          <div className="space-y-2 p-6">
            <p className="text-sm font-semibold tracking-wide text-brand uppercase">Featured moment</p>
            <h2 id="featured" className="font-display text-2xl font-semibold">
              Morning routines
            </h2>
            <p className="text-muted">
              Before lessons start, our students gather on the field for morning aerobics. It&apos;s more than exercise: it&apos;s how we build energy, teamwork and a joyful start to the school day.
            </p>
          </div>
        </section>
      )}
      {gallery.ok && gallery.data.categories.length > 1 && (
        <nav aria-label="Albums" className="mt-5 flex flex-wrap gap-2">
          {[undefined, ...gallery.data.categories].map((c) => (
            <Link
              key={c ?? "all"}
              href={c ? `/gallery?category=${encodeURIComponent(c)}` : "/gallery"}
              aria-current={category === c ? "page" : undefined}
              className={cn("inline-flex min-h-9 items-center rounded-full border px-4 text-sm", category === c ? "border-brand bg-brand/10 font-medium text-brand" : "border-border hover:bg-surface-2")}
            >
              {c ?? "All photos"}
            </Link>
          ))}
        </nav>
      )}
      <div className="mt-6">
        {!gallery.ok ? (
          <Card>
            <ProblemState problem={gallery.problem} />
          </Card>
        ) : photos.length === 0 && category ? (
          <Card>
            <EmptyState icon={ImageIcon} title="No photos in this album yet">
              Check back soon.
            </EmptyState>
          </Card>
        ) : photos.length === 0 ? (
          // Until the school uploads photos (Console → Website → Gallery), show the campus.
          <GalleryGrid
            photos={[siteImages.campus, siteImages.earlyYears, siteImages.coveredPlay, siteImages.culture, siteImages.courtyard, siteImages.pitch, siteImages.teaching].map((img) => ({
              id: img.src,
              title: img.alt,
              caption: null,
              category: "Campus",
              image: img.src,
            }))}
          />
        ) : (
          <GalleryGrid photos={photos} />
        )}
      </div>
    </div>
  );
}
