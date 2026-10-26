import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { loadSite } from "@/lib/site/api";
import { formatDay } from "@/lib/site/format";
import { RichContent } from "@/components/website/rich-content";

type Props = { params: Promise<{ slug: string }> };

async function newsletter(slug: string) {
  if (!/^[a-z0-9-]{1,120}$/.test(slug)) return null;
  const r = await loadSite((api) => api.GET("/api/public/newsletters/{slug}", { params: { path: { slug } } }));
  return r.ok ? r.data : null;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const n = await newsletter((await params).slug);
  return n ? { title: n.title, description: n.preheader ?? undefined } : { title: "Newsletter" };
}

/** E12.6: one published newsletter, as sent. */
export default async function NewsletterArticlePage({ params }: Props) {
  const n = await newsletter((await params).slug);
  if (!n) notFound();
  return (
    <article className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <Link href="/newsletters" className="mb-4 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        All newsletters
      </Link>
      <p className="text-sm text-muted">
        <time dateTime={n.sentAt}>{formatDay(n.sentAt)}</time>
      </p>
      <h1 className="mt-1 font-display text-4xl font-semibold text-balance">{n.title}</h1>
      {n.preheader && <p className="mt-3 text-lg text-muted">{n.preheader}</p>}
      <RichContent html={n.bodyHtml} className="mt-6 text-base" />
    </article>
  );
}
