import type { Metadata } from "next";
import { KioskScreen } from "@/components/clock/kiosk-screen";
import { publicSiteOrigin } from "@/lib/clock/origin";

export const metadata: Metadata = { title: "Staff clock-in", robots: { index: false, follow: false }, referrer: "no-referrer" };

/** The reception tablet's QR screen. No sign-in: the kiosk link's secret token is the credential. */
export default async function KioskPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const raw = (await searchParams).token;
  const token = (Array.isArray(raw) ? raw[0] : raw) ?? "";
  return <KioskScreen token={token} publicOrigin={publicSiteOrigin()} />;
}
