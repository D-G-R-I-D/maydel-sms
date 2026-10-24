"use client";

import { useRouter } from "next/navigation";
import { useState, useSyncExternalStore, useTransition } from "react";
import { Copy, ExternalLink, KeyRound, LocateFixed, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { issueKioskLink, saveClockSettings } from "@/lib/clock/actions";
import { hhmm, locationError, toApiTime, type ClockSettings } from "@/lib/clock/clock";
import { Button, Card, Field, Input, buttonClasses, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";

const noSubscribe = () => () => {};
const num = (v: string) => (v.trim() === "" ? null : Number(v));

/** Settings → Staff clock-in: school hours, grace period and the optional location check. */
export function ClockSettingsForm({ initial }: { initial: ClockSettings }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [startsAt, setStartsAt] = useState(hhmm(initial.startsAt));
  const [endsAt, setEndsAt] = useState(hhmm(initial.endsAt));
  const [grace, setGrace] = useState(String(initial.graceMinutes));
  const [requireLocation, setRequireLocation] = useState(initial.requireLocation);
  const [lat, setLat] = useState(initial.latitude?.toString() ?? "");
  const [lon, setLon] = useState(initial.longitude?.toString() ?? "");
  const [radius, setRadius] = useState(String(initial.radiusMeters));
  const [locating, setLocating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const server = problem?.errors ?? {};

  const coord = (v: string, max: number, what: string) =>
    v === "" ? (requireLocation ? "Needed for the location check." : undefined) : Math.abs(Number(v)) <= max && !Number.isNaN(Number(v)) ? undefined : `A ${what} from -${max} to ${max}.`;
  const local = {
    startsAt: toApiTime(startsAt) ? undefined : "Enter a time.",
    endsAt: !toApiTime(endsAt) ? "Enter a time." : endsAt <= startsAt ? "The day must end after it starts." : undefined,
    grace: /^\d+$/.test(grace) && Number(grace) <= 180 ? undefined : "0 to 180 minutes.",
    lat: coord(lat, 90, "latitude") ?? (lat === "" && lon !== "" ? "Add the latitude too." : undefined),
    lon: coord(lon, 180, "longitude") ?? (lon === "" && lat !== "" ? "Add the longitude too." : undefined),
    radius: /^\d+$/.test(radius) && Number(radius) >= 30 && Number(radius) <= 5000 ? undefined : "30 to 5,000 metres.",
  };
  // Server field errors show until the next save; only the local checks block saving.
  const errors = {
    startsAt: local.startsAt ?? server.startsAt?.[0],
    endsAt: local.endsAt ?? server.endsAt?.[0],
    grace: local.grace ?? server.graceMinutes?.[0],
    lat: local.lat ?? server.latitude?.[0],
    lon: local.lon ?? server.longitude?.[0],
    radius: local.radius ?? server.radiusMeters?.[0],
  };
  const invalid = Object.values(local).some(Boolean);
  const otherProblem = problem && !problem.errors ? problemMessage(problem) : null;

  const locate = () => {
    if (!("geolocation" in navigator)) return toast.error(locationError(undefined));
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (p) => {
        setLat(p.coords.latitude.toFixed(6));
        setLon(p.coords.longitude.toFixed(6));
        setLocating(false);
        toast.success(`Location set (accurate to about ${Math.round(p.coords.accuracy)} m)`);
      },
      (e) => {
        setLocating(false);
        toast.error(locationError(e.code));
      },
      { enableHighAccuracy: true, timeout: 20_000 },
    );
  };

  const save = async () => {
    setProblem(null);
    setBusy(true);
    const r = await runAction(
      () =>
        saveClockSettings({
          startsAt: toApiTime(startsAt)!,
          endsAt: toApiTime(endsAt)!,
          graceMinutes: Number(grace),
          requireLocation,
          latitude: num(lat),
          longitude: num(lon),
          radiusMeters: Number(radius),
        }),
      { success: "Clock-in settings saved", onProblem: setProblem },
    );
    setBusy(false);
    if (r) startTransition(() => router.refresh());
  };

  return (
    <Card className="space-y-5 p-5">
      <h2 className="font-display text-lg font-semibold">School day</h2>
      {otherProblem && (
        <Alert tone="danger" title="Couldn't save">
          {otherProblem}
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-3">
        <Field id="clk-start" label="Day starts" error={errors.startsAt} hint="Abuja time (WAT)">
          <Input type="time" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} {...describedBy("clk-start", errors.startsAt, true)} />
        </Field>
        <Field id="clk-end" label="Day ends" error={errors.endsAt}>
          <Input type="time" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} {...describedBy("clk-end", errors.endsAt)} />
        </Field>
        <Field id="clk-grace" label="Grace (minutes)" error={errors.grace} hint={`Late after ${startsAt || "—"} + ${grace || 0} min`}>
          <Input value={grace} onChange={(e) => setGrace(e.target.value)} inputMode="numeric" className="tabular" {...describedBy("clk-grace", errors.grace, true)} />
        </Field>
      </div>

      <div className="space-y-4 border-t border-border pt-5">
        <span className="flex items-center gap-3 text-sm">
          <Switch checked={requireLocation} onCheckedChange={setRequireLocation} label="Check location when staff scan" />
          <span aria-hidden>{requireLocation ? "Staff must be at school to clock in or out" : "No location check"}</span>
        </span>
        <p className="text-sm text-muted">
          With the check on, a scan is refused when the phone is further than the radius from the school. Stand at the school entrance and use this device&apos;s location to set the point.
        </p>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field id="clk-lat" label="Latitude" error={errors.lat}>
            <Input value={lat} onChange={(e) => setLat(e.target.value)} inputMode="decimal" className="tabular" placeholder="9.0765" {...describedBy("clk-lat", errors.lat)} />
          </Field>
          <Field id="clk-lon" label="Longitude" error={errors.lon}>
            <Input value={lon} onChange={(e) => setLon(e.target.value)} inputMode="decimal" className="tabular" placeholder="7.3986" {...describedBy("clk-lon", errors.lon)} />
          </Field>
          <Field id="clk-radius" label="Radius (metres)" error={errors.radius}>
            <Input value={radius} onChange={(e) => setRadius(e.target.value)} inputMode="numeric" className="tabular" {...describedBy("clk-radius", errors.radius)} />
          </Field>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" size="sm" loading={locating} loadingText="Locating…" onClick={locate}>
            <LocateFixed className="size-4" aria-hidden />
            Use this device&apos;s location
          </Button>
          {lat && lon && !errors.lat && !errors.lon && (
            <a href={`https://www.google.com/maps?q=${Number(lat)},${Number(lon)}`} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 text-sm font-medium text-brand hover:underline">
              Check the point on a map
              <ExternalLink className="size-3.5" aria-hidden />
            </a>
          )}
        </div>
      </div>

      <div className="flex justify-end">
        <Button loading={busy} loadingText="Saving…" disabled={invalid} onClick={save}>
          <Save className="size-4" aria-hidden />
          Save
        </Button>
      </div>
    </Card>
  );
}

/**
 * The kiosk link: copy or open it any time. Issuing a new one stops the old screen at once.
 * `token` is null when no link exists yet, or when it was issued before links could be shown again.
 */
export function KioskLinkCard({ token, issued, publicOrigin }: { token: string | null; issued: boolean; publicOrigin: string | null }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [current, setCurrent] = useState(token);
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  // The page's own address is only known in the browser (null while rendering on the server).
  const pageOrigin = useSyncExternalStore(noSubscribe, () => window.location.origin, () => null);
  const origin = publicOrigin ?? pageOrigin;
  const link = current && origin ? `${origin}/clock/kiosk?token=${encodeURIComponent(current)}` : null;

  const issue = async () => {
    setConfirming(false);
    setBusy(true);
    const r = await runAction(() => issueKioskLink(), { success: issued ? "New kiosk link issued; the old one has stopped working" : "Kiosk link issued" });
    setBusy(false);
    if (r) {
      setCurrent(r.token);
      startTransition(() => router.refresh());
    }
  };

  const copy = async () => {
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link);
      toast.success("Link copied");
    } catch {
      toast.error("Couldn't copy. Select the link and copy it instead.");
    }
  };

  return (
    <Card className="space-y-4 p-5">
      <div className="flex items-start gap-3">
        <KeyRound className="mt-0.5 size-5 shrink-0 text-brand" aria-hidden />
        <div>
          <h2 className="font-display text-lg font-semibold">Kiosk screen</h2>
          <p className="text-sm text-muted">
            Open the kiosk link on a tablet or spare phone at the school entrance. It shows a QR code that changes every 30 seconds, so a photo of it shared on WhatsApp stops working quickly.
          </p>
        </div>
      </div>

      {link ? (
        <div className="space-y-2">
          <Input readOnly value={link} onFocus={(e) => e.currentTarget.select()} className="w-full font-mono text-xs" aria-label="Kiosk link" />
          <div className="flex flex-wrap gap-2">
            <Button size="sm" onClick={copy}>
              <Copy className="size-4" aria-hidden />
              Copy link
            </Button>
            <a href={link} target="_blank" rel="noopener noreferrer" className={buttonClasses("secondary", "sm")}>
              <ExternalLink className="size-4" aria-hidden />
              Open kiosk
            </a>
          </div>
          <p className="text-xs text-muted">Keep it to the kiosk device: anyone with this link can show the clock-in code.</p>
        </div>
      ) : (
        <p className="text-sm">
          {issued ? "A kiosk link is active but was made before links could be shown again. Issue a new one once, and you can copy it any time after." : "No kiosk link yet."}
        </p>
      )}

      {confirming ? (
        <Alert tone="warning" title="Replace the kiosk link?">
          <p className="mt-1 text-sm">The screen using the current link will stop showing codes straight away. Use this if the link was shared with someone it shouldn&apos;t have been.</p>
          <div className="mt-2 flex gap-2">
            <Button size="sm" variant="destructive" onClick={issue}>
              Replace link
            </Button>
            <Button size="sm" variant="secondary" onClick={() => setConfirming(false)}>
              Cancel
            </Button>
          </div>
        </Alert>
      ) : (
        <Button variant={issued ? "secondary" : "primary"} loading={busy} loadingText="Issuing…" onClick={() => (issued ? setConfirming(true) : void issue())}>
          <KeyRound className="size-4" aria-hidden />
          {issued ? "Issue a new link" : "Issue kiosk link"}
        </Button>
      )}
    </Card>
  );
}
