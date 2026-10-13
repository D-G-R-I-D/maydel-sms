"use client";

import { useState } from "react";
import { KeyRound } from "lucide-react";
import type { IssuedCredentials } from "@/lib/api/types";
import { resetStaffPassword } from "@/lib/staff/actions";
import { runAction } from "@/lib/client/action";
import { problemMessage } from "@/lib/api/problem";
import { Button } from "@/components/ui/primitives";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { CredentialSlips } from "@/components/credentials/credential-slip";
import { toast } from "@/components/ui/toast";

/** Confirm → reset → one-time credential slip. The password lives only in this component's state. */
export function ResetPasswordButton({ staffId, fullName, description }: { staffId: string; fullName: string; description?: string }) {
  const [issued, setIssued] = useState<IssuedCredentials | null>(null);

  return (
    <>
      <ConfirmDialog
        title={`Reset ${fullName}'s password?`}
        confirmLabel="Reset password"
        tone="destructive"
        trigger={
          <Button variant="secondary">
            <KeyRound className="size-4" aria-hidden />
            Reset password
          </Button>
        }
        onConfirm={async () => {
          const creds = await runAction(() => resetStaffPassword(staffId), {
            success: "Password reset",
            onProblem: (p) => {
              if (p.status > 0 && p.status < 500 && p.status !== 429) toast.error("Couldn't reset the password", { description: problemMessage(p) });
            },
          });
          if (creds) setIssued(creds);
          return creds !== null;
        }}
      >
        <p>They&apos;ll get a new temporary password and be signed out everywhere. You&apos;ll see the password once, to print or hand over.</p>
      </ConfirmDialog>
      <FormDialog open={issued !== null} onOpenChange={(v) => !v && setIssued(null)} title="New temporary password" description={fullName}>
        {(close) => issued && <CredentialSlips people={[{ fullName, description, ...issued }]} onDone={close} />}
      </FormDialog>
    </>
  );
}
