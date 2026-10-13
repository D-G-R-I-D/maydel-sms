import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { requireSessionUser } from "@/lib/auth/session";
import { homeFor } from "@/lib/auth/roles";
import { Alert } from "@/components/ui/states";
import { SignOutButton } from "@/components/shell/sign-out-button";
import { ChangePasswordForm } from "./change-password-form";

export const metadata: Metadata = { title: "Change password" };

export default async function ChangePasswordPage() {
  const user = await requireSessionUser();
  const forced = user.mustChangePassword;

  return (
    <>
      {!forced && (
        <Link href={homeFor(user.roles)} className="mb-6 inline-flex items-center gap-1.5 text-sm text-brand hover:underline">
          <ArrowLeft className="size-4" aria-hidden />
          Back
        </Link>
      )}
      <h1 className="font-display text-3xl font-semibold text-text">{forced ? "Set a new password" : "Change password"}</h1>
      <p className="mt-1 mb-6 text-sm text-muted">
        Signed in as <span className="font-medium text-text">{user.userName}</span>
      </p>
      {forced ? (
        <Alert tone="info" className="mb-5" title="One more step">
          Your account was set up with a temporary password. Choose your own before you continue.
        </Alert>
      ) : (
        <Alert tone="info" className="mb-5">
          Changing your password signs you out on your other devices.
        </Alert>
      )}
      <ChangePasswordForm />
      <div className="mt-8 border-t border-border pt-4 text-center">
        <SignOutButton variant="link" />
      </div>
    </>
  );
}
