import type { Metadata } from "next";
import Link from "next/link";
import { SearchX } from "lucide-react";
import { load } from "@/lib/api/server";
import { Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { PaymentOutcome } from "@/components/portal/payment-outcome";

export const metadata: Metadata = { title: "Payment" };

const REFERENCE = /^[A-Za-z0-9._-]{4,100}$/;

/** E10.5: where Paystack sends the parent back (`?reference=…&trxref=…`). */
export default async function PaymentReturnPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams;
  const raw = Array.isArray(sp.reference) ? sp.reference[0] : (sp.reference ?? (Array.isArray(sp.trxref) ? sp.trxref[0] : sp.trxref));
  const header = <PageHeader title="Payment" />;
  const back = (
    <Link href="/portal/fees" className={buttonClasses("secondary")}>
      Back to fees
    </Link>
  );

  if (!raw || !REFERENCE.test(raw)) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={SearchX} title="No payment to show" action={back}>
            This page opens after paying online.
          </EmptyState>
        </Card>
      </>
    );
  }

  const result = await load((api) => api.GET("/api/portal/payments/{reference}", { params: { path: { reference: raw } } }));
  if (!result.ok) {
    return (
      <>
        {header}
        <Card>
          {result.problem.status === 404 ? (
            <EmptyState icon={SearchX} title="Payment not found" action={back}>
              We couldn&apos;t find that payment for your account.
            </EmptyState>
          ) : (
            <ProblemState problem={result.problem} />
          )}
        </Card>
      </>
    );
  }
  return (
    <>
      {header}
      <PaymentOutcome initial={result.data} />
    </>
  );
}
