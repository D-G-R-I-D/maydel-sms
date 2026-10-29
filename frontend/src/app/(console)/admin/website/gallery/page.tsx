import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { Images, Star } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { siteImage } from "@/lib/site/images";
import { Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { GalleryUploader, PhotoActions } from "@/components/website/gallery-admin";

export const metadata: Metadata = { title: "Gallery" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E7 console: the website gallery by album; upload several photos at once, feature some on the carousel. */
export default async function GalleryAdminPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Gallery" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const album = one((await searchParams).album);
  const list = await load((api) => api.GET("/api/cms/gallery"));
  const photos = list.ok ? list.data : [];
  const albums = [...new Set(photos.map((p) => p.category))].sort((a, b) => a.localeCompare(b));
  const shown = (album ? photos.filter((p) => p.category === album) : photos).sort((a, b) => a.category.localeCompare(b.category) || a.order - b.order);
  const nextOrder = Math.max(0, ...photos.map((p) => p.order)) + 1;
  const featured = photos.filter((p) => p.isFeatured).length;

  return (
    <>
      <PageHeader title="Gallery" description={`Photos on the website, in albums. Starred photos (${featured}) also appear on the homepage carousel.`} />
      <div className="space-y-5">
        <GalleryUploader albums={albums} nextOrder={nextOrder} />
        {!list.ok ? (
          <Card>
            <ProblemState problem={list.problem} />
          </Card>
        ) : photos.length === 0 ? (
          <Card>
            <EmptyState icon={Images} title="No photos yet">
              Add the first album above.
            </EmptyState>
          </Card>
        ) : (
          <>
            <nav aria-label="Albums" className="flex flex-wrap gap-2">
              {[undefined, ...albums].map((a) => (
                <Link
                  key={a ?? "all"}
                  href={a ? `/admin/website/gallery?album=${encodeURIComponent(a)}` : "/admin/website/gallery"}
                  aria-current={album === a ? "page" : undefined}
                  className={cn("inline-flex min-h-9 items-center rounded-full border px-3 text-sm", album === a ? "border-brand bg-brand/10 font-medium text-brand" : "border-border text-muted hover:text-text")}
                >
                  {a ?? "All albums"}
                  <span className="tabular ml-1.5 text-xs opacity-70">{a ? photos.filter((p) => p.category === a).length : photos.length}</span>
                </Link>
              ))}
            </nav>
            <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
              {shown.map((p) => {
                const img = siteImage(p.imageUrl);
                return (
                  <li key={p.id}>
                    <Card className="overflow-hidden p-0">
                      <div className="relative aspect-[4/3] bg-surface-2">
                        {img && <Image src={img} alt={p.title} fill sizes="(min-width: 1024px) 260px, 50vw" className="object-cover" />}
                        {p.isFeatured && (
                          <span className="absolute top-2 left-2 inline-flex items-center gap-1 rounded-full bg-black/60 px-2 py-0.5 text-xs text-white">
                            <Star className="size-3 fill-gold text-gold" aria-hidden />
                            Carousel
                          </span>
                        )}
                      </div>
                      <div className="flex items-start gap-1 p-3">
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm font-medium">{p.title}</p>
                          <p className="truncate text-xs text-muted">{p.caption ?? p.category}</p>
                        </div>
                        <PhotoActions photo={p} albums={albums} />
                      </div>
                    </Card>
                  </li>
                );
              })}
            </ul>
          </>
        )}
      </div>
    </>
  );
}
