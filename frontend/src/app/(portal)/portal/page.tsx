import type { Metadata } from "next";
import { CalendarCheck, ClipboardList, Wallet, type LucideIcon } from "lucide-react";
import { load } from "@/lib/api/server";
import { firstName } from "@/lib/flash";
import { Card, PageHeader } from "@/components/ui/primitives";
import { ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Home" };

/** Parent home answers three questions (design system §5); the data arrives with Phase 1 APIs. */
const glance: { icon: LucideIcon; title: string; body: string }[] = [
  { icon: ClipboardList, title: "How is my child doing?", body: "Term results and report cards will appear here once published." },
  { icon: Wallet, title: "Do I owe anything?", body: "Fee invoices and receipts will appear here." },
  { icon: CalendarCheck, title: "Is anything happening?", body: "Attendance and school notices will appear here." },
];

export default async function PortalHomePage() {
  const me = await load((api) => api.GET("/api/auth/me"));
  if (!me.ok) return <ProblemState problem={me.problem} />;
  const name = firstName(me.data.fullName) || me.data.fullName;
  const isParent = me.data.roles.includes("Parent");

  return (
    <>
      <PageHeader title={`Welcome, ${name}`} description={isParent ? "Everything about your children at Maydel, in one place." : "Your results, attendance and school news."} />
      <div className="grid gap-4 md:grid-cols-3">
        {glance.map(({ icon: Icon, title, body }) => (
          <Card key={title} className="p-5">
            <span className="mb-3 inline-flex size-10 items-center justify-center rounded-full bg-surface-2 text-brand">
              <Icon className="size-5" aria-hidden />
            </span>
            <h2 className="font-display text-lg font-semibold">{title}</h2>
            <p className="mt-1 text-sm text-muted">{body}</p>
          </Card>
        ))}
      </div>
    </>
  );
}
