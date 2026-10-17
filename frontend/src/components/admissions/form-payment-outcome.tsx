"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { CheckCircle2, Clock, ShieldAlert, XCircle } from "lucide-react";
import { checkFormPayment } from "@/lib/admissions/actions";
import { Card, buttonClasses } from "@/components/ui/primitives";
import { SchoolLoader } from "@/components/ui/school-loader";

type Outcome = { reference: string; status: "Pending" | "Succeeded" | "Failed" | "NeedsReview"; applicationId: string; message: string | null };

const POLL_MS = 3_000;
const POLL_FOR_MS = 30_000;

/** After Paystack sends the family back (E11.4): paid / waiting (polls ~30 s) / needs review / failed. */
export function FormPaymentOutcome({ initial }: { initial: Outcome }) {
  const router = useRouter();
  const [result, setResult] = useState(initial);
  const [gaveUp, setGaveUp] = useState(false);
  const started = useRef<number | null>(null);

  useEffect(() => {
    if (result.status !== "Pending") {
      router.refresh();
      return;
    }
    started.current ??= Date.now();
    const t = setInterval(async () => {
      if (Date.now() - (started.current ?? 0) > POLL_FOR_MS) {
        clearInterval(t);
        setGaveUp(true);
        return;
      }
      try {
        const r = await checkFormPayment(result.reference);
        if (r.ok) setResult(r.data as Outcome);
      } catch {
        // offline: try again on the next tick
      }
    }, POLL_MS);
    return () => clearInterval(t);
  }, [result.status, result.reference, router]);

  const back = (
    <Link href="/admissions" className={buttonClasses(result.status === "Succeeded" ? "primary" : "secondary")}>
      {result.status === "Succeeded" ? "Continue to your application" : "Back to your applications"}
    </Link>
  );

  const view =
    result.status === "Succeeded"
      ? { icon: <CheckCircle2 className="mx-auto size-12 text-success" aria-hidden />, title: "Form paid", body: "Thank you. We've emailed you the next steps: fill in the form and book the entrance exam." }
      : result.status === "Pending"
        ? {
            icon: gaveUp ? <Clock className="mx-auto size-12 text-muted" aria-hidden /> : <SchoolLoader className="mx-auto" />,
            title: gaveUp ? "Still waiting for Paystack" : "Waiting for Paystack to confirm…",
            body: gaveUp ? "We'll update your application as soon as Paystack confirms. You can safely leave this page." : "This usually takes a few seconds.",
          }
        : result.status === "NeedsReview"
          ? { icon: <ShieldAlert className="mx-auto size-12 text-warning" aria-hidden />, title: "We need to check this payment", body: result.message ?? "The school will contact you." }
          : { icon: <XCircle className="mx-auto size-12 text-danger" aria-hidden />, title: "Payment didn't go through", body: result.message ?? "No money was taken. You can try again." };

  return (
    <Card className="p-6 text-center" aria-live="polite">
      {view.icon}
      <h2 className="mt-3 font-display text-2xl font-semibold">{view.title}</h2>
      <p className="mt-1 text-sm text-muted">{view.body}</p>
      <div className="mt-5 flex justify-center">{back}</div>
    </Card>
  );
}
