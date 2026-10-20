import type { Metadata } from "next";
import { Clock, Mail, MapPin, MessageCircle, Phone } from "lucide-react";
import { loadSchoolProfile, telHref } from "@/lib/site/api";
import { ContactForm } from "@/components/site/contact-form";
import { Card, buttonClasses } from "@/components/ui/primitives";

export const metadata: Metadata = { title: "Contact us", description: "Contact Maydel Spring Schools, Abuja: address, phone, WhatsApp, email and a message form." };

/** E7: contact details from Settings → School, a map link, WhatsApp and the message form. */
export default async function ContactPage() {
  const p = await loadSchoolProfile();
  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="font-display text-4xl font-semibold">Contact us</h1>
      <p className="mt-1 text-muted">We&apos;d love to hear from you. Send a message, call, or visit the school.</p>
      <div className="mt-6 grid gap-6 lg:grid-cols-[2fr_1fr] lg:items-start">
        <Card className="p-5 sm:p-6">
          <h2 className="mb-4 font-display text-xl font-semibold">Send us a message</h2>
          <ContactForm />
        </Card>
        <Card className="space-y-4 p-5">
          <h2 className="font-display text-xl font-semibold">School information</h2>
          <address className="space-y-3 text-sm not-italic">
            {p.address && (
              <p className="flex gap-2">
                <MapPin className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
                {p.address}
              </p>
            )}
            {p.phone && (
              <p className="flex gap-2">
                <Phone className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
                <a href={telHref(p.phone)} className="text-brand hover:underline">
                  {p.phone}
                </a>
              </p>
            )}
            {p.email && (
              <p className="flex gap-2">
                <Mail className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
                <a href={`mailto:${p.email}`} className="break-all text-brand hover:underline">
                  {p.email}
                </a>
              </p>
            )}
            {p.officeHours && (
              <p className="flex gap-2">
                <Clock className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden />
                {p.officeHours}
              </p>
            )}
          </address>
          <div className="flex flex-wrap gap-2">
            {p.whatsAppUrl && (
              <a href={p.whatsAppUrl} target="_blank" rel="noopener noreferrer" className={buttonClasses("primary", "sm")}>
                <MessageCircle className="size-4" aria-hidden />
                Chat on WhatsApp
              </a>
            )}
            {p.mapUrl && (
              <a href={p.mapUrl} target="_blank" rel="noopener noreferrer" className={buttonClasses("secondary", "sm")}>
                <MapPin className="size-4" aria-hidden />
                Open in Google Maps
              </a>
            )}
          </div>
          {!p.address && !p.phone && !p.email && <p className="text-sm text-muted">Use the form and the school office will get back to you.</p>}
        </Card>
      </div>
    </div>
  );
}
