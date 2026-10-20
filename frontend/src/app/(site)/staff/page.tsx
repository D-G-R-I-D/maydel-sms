import type { Metadata } from "next";
import Image from "next/image";
import { Users } from "lucide-react";
import { loadSite, siteImage } from "@/lib/site/api";
import { Avatar, Card } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Our staff", description: "Meet the teachers and staff of Maydel Spring Schools, Abuja." };

/** E7: the staff the school has chosen to show on the website, in its order. */
export default async function StaffPage() {
  const staff = await loadSite((api) => api.GET("/api/public/site/staff"));
  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="font-display text-4xl font-semibold">Our staff</h1>
      <p className="mt-1 max-w-2xl text-muted">The people who make Maydel: qualified, dedicated teachers who know every child by name.</p>
      <div className="mt-6">
        {!staff.ok ? (
          <Card>
            <ProblemState problem={staff.problem} />
          </Card>
        ) : staff.data.length === 0 ? (
          <Card>
            <EmptyState icon={Users} title="Coming soon">
              Staff profiles will appear here.
            </EmptyState>
          </Card>
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {staff.data.map((s) => {
              const photo = siteImage(s.photoUrl);
              return (
                <li key={s.id}>
                  <Card className="flex h-full gap-4 p-5">
                    {photo ? (
                      <Image src={photo} alt="" width={80} height={80} className="size-20 shrink-0 rounded-full object-cover" />
                    ) : (
                      <Avatar name={s.name} className="size-20 shrink-0 text-xl" />
                    )}
                    <div className="min-w-0">
                      <h2 className="font-display text-lg font-semibold">{s.name}</h2>
                      {s.jobTitle && <p className="text-sm text-brand">{s.jobTitle}</p>}
                      {s.bio && <p className="mt-2 text-sm whitespace-pre-line text-muted">{s.bio}</p>}
                    </div>
                  </Card>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
