"use client";

import { useState } from "react";
import { KeyRound } from "lucide-react";
import type { IssuedCredentials } from "@/lib/api/types";
import { problemMessage } from "@/lib/api/problem";
import type { Loaded } from "@/lib/api/server";
import { resetStaffPassword } from "@/lib/staff/actions";
import { issueGuardianLogin, issueStudentLogin } from "@/lib/students/actions";
import { runAction } from "@/lib/client/action";
import { Button } from "@/components/ui/primitives";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";
import { CredentialSlips } from "./credential-slip";

type Kind = "staff" | "student" | "guardian";

/** Normalises the three login endpoints to "credentials, or linked to an existing account". */
async function issue(kind: Kind, id: string): Promise<Loaded<{ credentials: IssuedCredentials | null }>> {
  if (kind === "guardian") {
    const r = await issueGuardianLogin(id);
    return r.ok ? { ok: true, data: { credentials: r.data.credentials } } : r;
  }
  const r = kind === "staff" ? await resetStaffPassword(id) : await issueStudentLogin(id);
  return r.ok ? { ok: true, data: { credentials: r.data } } : r;
}

/**
 * Confirm → issue (or reset) a login → one-time credential slip. The temporary password lives only
 * in this component's state and is dropped when the slip closes.
 */
export function IssueLoginButton({
  kind,
  id,
  fullName,
  description,
  hasLogin,
  size = "md",
  variant = "secondary",
}: {
  kind: Kind;
  id: string;
  fullName: string;
  description?: string;
  hasLogin: boolean;
  size?: "sm" | "md";
  variant?: "secondary" | "ghost";
}) {
  const [issued, setIssued] = useState<IssuedCredentials | null>(null);
  const action = kind === "guardian" ? (hasLogin ? "Reset parent password" : "Issue parent login") : hasLogin ? "Reset password" : "Issue login";

  return (
    <>
      <ConfirmDialog
        title={hasLogin ? `Reset ${fullName}'s password?` : `Issue a login for ${fullName}?`}
        confirmLabel={action}
        tone={hasLogin ? "destructive" : "primary"}
        trigger={
          <Button variant={variant} size={size}>
            <KeyRound className="size-4" aria-hidden />
            {action}
          </Button>
        }
        onConfirm={async () => {
          const result = await runAction(() => issue(kind, id), {
            success: hasLogin ? "Password reset" : "Login issued",
            onProblem: (p) => {
              if (p.status > 0 && p.status < 500 && p.status !== 429) toast.error(`Couldn't ${action.toLowerCase()}`, { description: problemMessage(p) });
            },
          });
          if (!result) return false;
          if (result.credentials) setIssued(result.credentials);
          else toast.info("Linked to their existing account", { description: "They already have a login (e.g. as staff) and use their current password." });
          return true;
        }}
      >
        {hasLogin ? (
          <p>They&apos;ll get a new temporary password and be signed out everywhere. You&apos;ll see it once, to print or hand over.</p>
        ) : (
          <p>
            A temporary password is created and shown once.
            {kind === "student" && " Their username is their admission number."}
            {kind === "guardian" && " Their username is their email, or their phone number if they have no email. One login covers all their children."}
          </p>
        )}
      </ConfirmDialog>
      <FormDialog open={issued !== null} onOpenChange={(v) => !v && setIssued(null)} title="Sign-in details" description={fullName}>
        {(close) => issued && <CredentialSlips people={[{ fullName, description, ...issued }]} onDone={close} />}
      </FormDialog>
    </>
  );
}
