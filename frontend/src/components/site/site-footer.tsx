import Link from "next/link";
import { Clock, Mail, MapPin, MessageCircle, Phone } from "lucide-react";
import { siteNav } from "@/lib/site/content";
import { telHref, type SchoolProfile } from "@/lib/site/api";
import { NewsletterSignup } from "./newsletter-signup";

/** E7: contact details from Settings → School (never hard-coded), quick links and the portal. */
export function SiteFooter({ profile }: { profile: SchoolProfile }) {
  const year = new Date().getFullYear();
  return (
    <footer className="mt-16 border-t border-border bg-surface">
      <div className="mx-auto grid w-full max-w-6xl gap-8 px-4 py-10 sm:grid-cols-2 sm:px-6 lg:grid-cols-3">
        <div className="space-y-2">
          <p className="font-display text-lg font-semibold text-brand">{profile.name}</p>
          {profile.motto && <p className="text-sm text-muted">{profile.motto}</p>}
          <p className="text-sm text-muted">An approved WAEC and NECO examination centre.</p>
          <div className="pt-3">
            <NewsletterSignup />
          </div>
        </div>
        <address className="space-y-2 text-sm not-italic">
          {profile.address && (
            <p className="flex gap-2">
              <MapPin className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
              {profile.mapUrl ? (
                <a href={profile.mapUrl} target="_blank" rel="noopener noreferrer" className="hover:text-brand hover:underline">
                  {profile.address}
                </a>
              ) : (
                profile.address
              )}
            </p>
          )}
          {profile.phone && (
            <p className="flex gap-2">
              <Phone className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
              <a href={telHref(profile.phone)} className="hover:text-brand hover:underline">
                {profile.phone}
              </a>
            </p>
          )}
          {profile.whatsAppUrl && (
            <p className="flex gap-2">
              <MessageCircle className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
              <a href={profile.whatsAppUrl} target="_blank" rel="noopener noreferrer" className="hover:text-brand hover:underline">
                WhatsApp{profile.whatsApp ? ` ${profile.whatsApp}` : ""}
              </a>
            </p>
          )}
          {profile.email && (
            <p className="flex gap-2">
              <Mail className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
              <a href={`mailto:${profile.email}`} className="break-all hover:text-brand hover:underline">
                {profile.email}
              </a>
            </p>
          )}
          {profile.officeHours && (
            <p className="flex gap-2">
              <Clock className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
              {profile.officeHours}
            </p>
          )}
        </address>
        <nav aria-label="Footer" className="text-sm">
          <ul className="grid grid-cols-2 gap-x-4 gap-y-2">
            {siteNav.map((n) => (
              <li key={n.href}>
                <Link href={n.href} className="hover:text-brand hover:underline">
                  {n.label}
                </Link>
              </li>
            ))}
            <li>
              <Link href="/newsletters" className="hover:text-brand hover:underline">
                Newsletters
              </Link>
            </li>
            <li>
              <Link href="/admissions/apply" className="hover:text-brand hover:underline">
                Buy admission form
              </Link>
            </li>
            <li>
              <Link href="/login" className="hover:text-brand hover:underline">
                Portal sign-in
              </Link>
            </li>
          </ul>
        </nav>
      </div>
      <p className="border-t border-border px-4 py-4 text-center text-xs text-muted">
        © {year} {profile.name}. All rights reserved.
      </p>
    </footer>
  );
}
