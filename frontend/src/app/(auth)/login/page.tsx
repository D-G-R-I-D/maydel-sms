import Link from "next/link";
import type { Metadata } from "next";
import { googleClientId, showDemoAccounts } from "@/lib/api/config";
import { safeNext } from "@/lib/auth/roles";
import { Alert } from "@/components/ui/states";
import { LoginForm } from "./login-form";

export const metadata: Metadata = { title: "Sign in" };

const reasons: Record<string, { tone: "info" | "warning"; text: string }> = {
  expired: { tone: "info", text: "Your session has ended. Please sign in again." },
  busy: { tone: "warning", text: "Too many attempts. Please wait a minute and try again." },
  unavailable: { tone: "warning", text: "We couldn't reach the school system. Please try again shortly." },
};

export default async function LoginPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);
  const next = safeNext(one(params.next));
  const reason = reasons[one(params.reason) ?? ""];

  return (
    <>
      <h1 className="font-display text-3xl font-semibold text-text">Sign in</h1>
      <p className="mt-1 mb-6 text-sm text-muted">Staff, parents and students use the same page.</p>
      {reason && (
        <Alert tone={reason.tone} className="mb-5">
          {reason.text}
        </Alert>
      )}
      <LoginForm next={next} googleClientId={googleClientId()} showDemo={showDemoAccounts()} />
      <p className="mt-6 text-center text-sm text-muted">
        Applying for a place?{" "}
        <Link href="/admissions/apply" className="font-medium text-brand hover:underline">
          Buy the admission form
        </Link>
      </p>
    </>
  );
}
