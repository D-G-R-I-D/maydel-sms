import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { RichContent } from "@/components/website/rich-content";
import { notFound } from "next/navigation";
import { ArrowLeft, ArrowRight } from "lucide-react";
import { loadSite, siteImage } from "@/lib/site/api";
import { formatDay } from "@/lib/site/format";
import { buttonClasses } from "@/components/ui/primitives";

type Props = { params: Promise<{ slug: string }> };
const validSlug = (s: string) => /^[a-z0-9][a-z0-9-]{0,200}$/.test(s);

async function article(slug: string) {
  if (!validSlug(slug)) return null;
  const r = await loadSite((api) => api.GET("/api/public/site/news/{slug}", { params: { path: { slug } } }));
  return r.ok ? r.data : null;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const a = await article((await params).slug);
  if (!a) return { title: "News" };
  const img = siteImage(a.imageUrl);
  return {
    title: a.title,
    description: a.summary ?? a.body.slice(0, 160),
    alternates: { canonical: `/news/${a.slug}` },
    openGraph: { type: "article", title: a.title, description: a.summary ?? undefined, publishedTime: a.publishedAt, ...(img ? { images: [img] } : {}) },
  };
}

/** E7: one published news article: formatted HTML from the editor, or plain text where blank lines become paragraphs. */
export default async function NewsArticlePage({ params }: Props) {
  const a = await article((await params).slug);
  if (!a) notFound();
  const img = siteImage(a.imageUrl);
  const paragraphs = a.body.split(/\n\s*\n/).map((p) => p.trim()).filter(Boolean);

  return (
    <article className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <Link href="/news" className="mb-4 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        All news
      </Link>
      <p className="text-sm text-muted">
        {a.category && <span>{a.category} · </span>}
        <time dateTime={a.publishedAt}>{formatDay(a.publishedAt)}</time>
      </p>
      <h1 className="mt-1 font-display text-4xl font-semibold text-balance">{a.title}</h1>
      {a.summary && <p className="mt-3 text-lg text-muted">{a.summary}</p>}
      {img && (
        <div className="relative mt-6 aspect-[16/9] overflow-hidden rounded-card bg-surface-2">
          <Image src={img} alt="" fill priority sizes="(min-width: 768px) 720px, 100vw" className="object-cover" />
        </div>
      )}
      {a.bodyHtml ? (
        <RichContent html={a.bodyHtml} className="mt-6 text-base" />
      ) : (
        <div className="mt-6 space-y-4 text-base leading-relaxed">
          {paragraphs.map((p, i) => (
            <p key={i} className="whitespace-pre-line">
              {p}
            </p>
          ))}
        </div>
      )}
      {a.linkUrl && (
        <Link href={a.linkUrl} className={`${buttonClasses("primary")} mt-8`}>
          {a.linkText || "Learn more"}
          <ArrowRight className="size-4" aria-hidden />
        </Link>
      )}
    </article>
  );
}
