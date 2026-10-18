import type { Metadata } from "next";
import Link from "next/link";
import { SearchX } from "lucide-react";
import { load } from "@/lib/api/server";
import { Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { FormPaymentOutcome } from "@/components/admissions/form-payment-outcome";

export const metadata: Metadata = { title: "Form payment" };

const REFERENCE = /^[A-Za-z0-9._-]{4,100}$/;

/** E11.4: where Paystack sends the family back after paying for the admission form. */
export default async function FormPaidPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams;
  const raw = Array.isArray(sp.reference) ? sp.reference[0] : (sp.reference ?? (Array.isArray(sp.trxref) ? sp.trxref[0] : sp.trxref));
  const back = (
    <Link href="/admissions" className={buttonClasses("secondary")}>
      Back to your applications
    </Link>
  );
  const header = <PageHeader title="Form payment" />;
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
  const result = await load((api) => api.GET("/api/admissions/payments/{reference}", { params: { path: { reference: raw } } }));
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
      <FormPaymentOutcome initial={result.data as Parameters<typeof FormPaymentOutcome>[0]["initial"]} />
    </>
  );
}
