import type { Metadata } from "next";
import { loadSchoolProfile, loadSite } from "@/lib/site/api";
import { SiteHeader } from "@/components/site/site-header";
import { SiteFooter } from "@/components/site/site-footer";
import { NoticesEnvelope } from "@/components/site/notices-envelope";
import { WhatsAppButton } from "@/components/site/whatsapp-button";
import { NewsletterNudge } from "@/components/site/newsletter-nudge";

export const metadata: Metadata = {
  title: { default: "Maydel Spring Schools, Abuja", template: "%s · Maydel Spring Schools" },
  description: "Maydel Spring Schools, Abuja: Creche to Senior Secondary, and an approved WAEC and NECO examination centre. The Well Spring of Knowledge.",
};

/** E7: the public website (www). Anonymous and cached; the app routes keep their own layouts. */
export default async function SiteLayout({ children }: { children: React.ReactNode }) {
  const [profile, notices] = await Promise.all([loadSchoolProfile(), loadSite((api) => api.GET("/api/public/site/notices"))]);
  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "School",
    name: profile.name,
    ...(profile.address ? { address: profile.address } : {}),
    ...(profile.phone ? { telephone: profile.phone } : {}),
    ...(profile.email ? { email: profile.email } : {}),
    ...(profile.website ? { url: profile.website } : {}),
    sameAs: [profile.facebookUrl, profile.instagramUrl].filter(Boolean),
  };
  return (
    <div className="flex min-h-dvh flex-col">
      <SiteHeader schoolName={profile.name} />
      <main id="main" className="flex-1">
        {children}
      </main>
      <SiteFooter profile={profile} />
      <WhatsAppButton href={profile.whatsAppUrl} />
      <NewsletterNudge />
      <NoticesEnvelope notices={notices.ok ? notices.data : []} raised={Boolean(profile.whatsAppUrl)} />
      {/* Structured data for search engines; built from Settings → School only. */}
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }} />
    </div>
  );
}
