"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState, useTransition } from "react";
import { CheckCircle2, Clock3, LogOut, MapPin, QrCode, TriangleAlert } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { unreachableProblem } from "@/lib/client/unreachable";
import { clock } from "@/lib/clock/actions";
import { ClockCodes, locationError, resultHeadline, type ClockResult } from "@/lib/clock/clock";
import { Button, Card } from "@/components/ui/primitives";

type Phase = { kind: "working"; label: string } | { kind: "done"; result: ClockResult } | { kind: "problem"; problem: Problem; needsLocation: boolean };

function position(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => {
    if (!("geolocation" in navigator)) reject(new Error("unsupported"));
    else navigator.geolocation.getCurrentPosition(resolve, reject, { enableHighAccuracy: true, timeout: 15_000, maximumAge: 30_000 });
  });
}

/**
 * Sends the scanned code once, as soon as the page opens. If the school requires location, it asks the
 * browser for it and tries again. The code is then dropped from the address bar so a reload can't resend it.
 */
export function ClockCard({ code }: { code: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [phase, setPhase] = useState<Phase>({ kind: "working", label: "Recording your scan…" });
  const sent = useRef(false);

  const send = useCallback(
    async (askLocation: boolean) => {
      // Without a location first; if the school requires one, ask the browser and send once more.
      for (let withLocation = askLocation; ; withLocation = true) {
        let lat: number | undefined;
        let lon: number | undefined;
        if (withLocation) {
          setPhase({ kind: "working", label: "Checking your location…" });
          try {
            const p = await position();
            lat = p.coords.latitude;
            lon = p.coords.longitude;
          } catch (error) {
            const code = error && typeof error === "object" && "code" in error ? Number((error as { code: unknown }).code) : undefined;
            setPhase({ kind: "problem", needsLocation: true, problem: { status: 400, title: "Location needed", detail: `The school needs your location to clock you in. ${locationError(code)}` } });
            return;
          }
        }
        setPhase({ kind: "working", label: "Recording your scan…" });
        let r: Awaited<ReturnType<typeof clock>>;
        try {
          r = await clock(code, lat, lon);
        } catch (error) {
          if (error && typeof error === "object" && "digest" in error && String((error as { digest: unknown }).digest).startsWith("NEXT_REDIRECT")) throw error;
          setPhase({ kind: "problem", problem: unreachableProblem(), needsLocation: false });
          return;
        }
        if (r.ok) {
          setPhase({ kind: "done", result: r.data });
          startTransition(() => router.replace("/clock", { scroll: false }));
          return;
        }
        const needsLocation = r.problem.status === 400 && Boolean(r.problem.errors?.latitude);
        if (needsLocation && !withLocation) continue;
        // A double submit means the first one landed: show the history instead of an error.
        if (r.problem.code === ClockCodes.twice) {
          startTransition(() => router.replace("/clock", { scroll: false }));
        }
        setPhase({ kind: "problem", problem: r.problem, needsLocation });
        return;
      }
    },
    [code, router],
  );

  useEffect(() => {
    if (sent.current) return;
    sent.current = true;
    void send(false);
  }, [send]);

  if (phase.kind === "working") {
    return (
      <Card className="flex items-center gap-4 p-6" role="status" aria-live="polite">
        <Clock3 className="size-8 shrink-0 animate-pulse text-brand motion-reduce:animate-none" aria-hidden />
        <p className="text-lg">{phase.label}</p>
      </Card>
    );
  }

  if (phase.kind === "done") {
    const { title, detail } = resultHeadline(phase.result);
    const Icon = phase.result.clockedIn ? CheckCircle2 : LogOut;
    const late = phase.result.clockedIn && phase.result.day.isLate;
    return (
      <Card className="flex items-start gap-4 p-6" role="status" aria-live="polite">
        <span className={`inline-flex size-12 shrink-0 items-center justify-center rounded-full ${late ? "bg-warning/15 text-warning" : "bg-success/15 text-success"}`}>
          <Icon className="size-7" aria-hidden />
        </span>
        <div>
          <p className="font-display text-2xl font-semibold">{title}</p>
          <p className="mt-1 text-muted">{detail}</p>
          <p className="mt-2 text-xs text-muted">Abuja time (WAT)</p>
        </div>
      </Card>
    );
  }

  const p = phase.problem;
  const expired = p.code === ClockCodes.code;
  return (
    <Card className="space-y-4 p-6" role="alert">
      <div className="flex items-start gap-4">
        <span className="inline-flex size-12 shrink-0 items-center justify-center rounded-full bg-danger/10 text-danger">
          {phase.needsLocation ? <MapPin className="size-7" aria-hidden /> : expired ? <QrCode className="size-7" aria-hidden /> : <TriangleAlert className="size-7" aria-hidden />}
        </span>
        <div>
          <p className="font-display text-xl font-semibold">{expired ? "That code has expired" : phase.needsLocation ? "Location needed" : "Not recorded"}</p>
          <p className="mt-1 text-muted">{expired ? "Codes change every 30 seconds. Scan the code on the school screen again." : problemMessage(p)}</p>
        </div>
      </div>
      {phase.needsLocation && (
        <Button onClick={() => void send(true)}>
          <MapPin className="size-4" aria-hidden />
          Share my location and try again
        </Button>
      )}
    </Card>
  );
}
