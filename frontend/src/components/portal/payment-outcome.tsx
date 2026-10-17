"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { CheckCircle2, Clock, ShieldAlert, XCircle } from "lucide-react";
import type { OnlinePaymentResult } from "@/lib/api/types";
import { formatNaira } from "@/lib/finance/rules";
import { checkOnlinePayment } from "@/lib/portal/actions";
import { Card, buttonClasses } from "@/components/ui/primitives";
import { SchoolLoader } from "@/components/ui/school-loader";
import { PdfDownloadButton } from "@/components/reports/pdf-download";

const POLL_MS = 3_000;
const POLL_FOR_MS = 30_000;

/** The outcome after Paystack sends the parent back (E10.5). Polls every 3 s for ~30 s while pending. */
export function PaymentOutcome({ initial }: { initial: OnlinePaymentResult }) {
  const router = useRouter();
  const [result, setResult] = useState(initial);
  const [gaveUp, setGaveUp] = useState(false);
  const started = useRef<number | null>(null);

  useEffect(() => {
    if (result.status !== "Pending") {
      router.refresh(); // fees/balances change once the payment is settled
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
        const r = await checkOnlinePayment(result.reference);
        if (r.ok) setResult(r.data);
      } catch {
        // offline: keep waiting; the next tick tries again
      }
    }, POLL_MS);
    return () => clearInterval(t);
  }, [result.status, result.reference, router]);

  const feesHref = `/portal/fees?child=${result.studentId}`;
  const back = (
    <Link href={feesHref} className={buttonClasses("secondary")}>
      Back to fees
    </Link>
  );

  if (result.status === "Succeeded")
    return (
      <Card className="p-6 text-center">
        <CheckCircle2 className="mx-auto size-12 text-success" aria-hidden />
        <h2 className="mt-3 font-display text-2xl font-semibold">Payment received</h2>
        <p className="tabular mt-1 text-lg">{formatNaira(result.amount)}</p>
        {result.receiptNumber && <p className="mt-1 text-sm text-muted">Receipt {result.receiptNumber}</p>}
        {result.excess > 0 && <p className="mt-2 text-sm text-warning">{formatNaira(result.excess)} more than was owed; the school will refund or reallocate it.</p>}
        <div className="mt-5 flex flex-wrap justify-center gap-2">
          {result.paymentId && (
            <PdfDownloadButton href={`/api/media/portal/children/${result.studentId}/receipts/${result.paymentId}/pdf`} label="Download receipt" fallbackName={`${result.receiptNumber ?? "Receipt"}.pdf`} showView={false} />
          )}
          {back}
        </div>
      </Card>
    );

  if (result.status === "Pending")
    return (
      <Card className="p-6 text-center" aria-live="polite">
        {gaveUp ? <Clock className="mx-auto size-12 text-muted" aria-hidden /> : <SchoolLoader className="mx-auto" />}
        <h2 className="mt-3 font-display text-2xl font-semibold">{gaveUp ? "Still waiting for Paystack" : "Waiting for Paystack to confirm…"}</h2>
        <p className="mt-1 text-sm text-muted">{gaveUp ? "We'll update your balance as soon as Paystack confirms. You can safely leave this page." : "This usually takes a few seconds."}</p>
        <div className="mt-5 flex justify-center">{back}</div>
      </Card>
    );

  if (result.status === "NeedsReview")
    return (
      <Card className="p-6 text-center">
        <ShieldAlert className="mx-auto size-12 text-warning" aria-hidden />
        <h2 className="mt-3 font-display text-2xl font-semibold">We need to check this payment</h2>
        <p className="mt-1 text-sm text-muted">We received your payment but need to check it. The school will contact you.</p>
        {result.message && <p className="mt-2 text-sm">{result.message}</p>}
        <div className="mt-5 flex justify-center">{back}</div>
      </Card>
    );

  return (
    <Card className="p-6 text-center">
      <XCircle className="mx-auto size-12 text-danger" aria-hidden />
      <h2 className="mt-3 font-display text-2xl font-semibold">Payment didn&apos;t go through</h2>
      <p className="mt-1 text-sm text-muted">{result.message ?? "No money was taken."}</p>
      <div className="mt-5 flex flex-wrap justify-center gap-2">
        <Link href={feesHref} className={buttonClasses("primary")}>
          Try again
        </Link>
      </div>
    </Card>
  );
}
