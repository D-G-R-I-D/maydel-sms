import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { Bus, Clock3 } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { firstName } from "@/lib/flash";
import { longDay } from "@/lib/clock/clock";
import { DriverDay, LiveClock } from "@/components/transport/driver-day";
import { ThemeToggle } from "@/components/shell/theme-toggle";
import { UserMenu } from "@/components/shell/user-menu";
import { Card, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "School bus", robots: { index: false, follow: false } };

/** E18: the driver's phone page. Today's checklist and Left / Back taps for each run; times in WAT. */
export default async function DriverPage() {
  const user = await requireSessionUser();
  const today = await load((api) => api.GET("/api/driver/today"));

  return (
    <div className="min-h-dvh bg-bg">
      <header className="sticky top-0 z-20 border-b border-border bg-surface/90 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-2xl items-center gap-3 px-4">
          <Image src="/brand/crest.svg" alt="" width={28} height={35} className="h-auto w-7" />
          <p className="min-w-0 flex-1 truncate font-display font-semibold text-brand">School bus</p>
          <ThemeToggle />
          <UserMenu fullName={user.fullName} userName={user.userName} roles={user.roles} />
        </div>
      </header>
      <main id="main" className="mx-auto max-w-2xl space-y-6 px-4 py-6">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="font-display text-2xl font-semibold">Good day, {firstName(user.fullName) || user.fullName}</h1>
            <p className="text-sm text-muted">{today.ok ? longDay(today.data.date) : "Today"}</p>
          </div>
          <p className="font-display text-3xl font-semibold" aria-label="Time now, Abuja">
            <LiveClock />
            <span className="ml-1 align-middle text-xs font-normal text-muted">WAT</span>
          </p>
        </div>
        {!today.ok ? (
          <Card>
            <ProblemState problem={today.problem} />
          </Card>
        ) : today.data.buses.length === 0 ? (
          <Card>
            <EmptyState icon={Bus} title="No bus assigned to you">
              Ask the school admin to assign you a bus in Transport → Setup.
            </EmptyState>
          </Card>
        ) : (
          <DriverDay buses={today.data.buses} questions={today.data.questions} />
        )}
        <Link href="/clock" className={buttonClasses("secondary")}>
          <Clock3 className="size-4" aria-hidden />
          My clock-in history
        </Link>
      </main>
    </div>
  );
}
