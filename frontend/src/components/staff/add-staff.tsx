"use client";

import Link from "next/link";
import { useState } from "react";
import { Plus, UserRound } from "lucide-react";
import type { StaffCreated } from "@/lib/api/types";
import { Alert } from "@/components/ui/states";
import { Button, buttonClasses } from "@/components/ui/primitives";
import { CancelButton, FormDialog } from "@/components/ui/dialog";
import { CredentialSlips } from "@/components/credentials/credential-slip";
import { StaffForm } from "./staff-form";

/** Add staff → credential slip (shown once) or "linked to existing account", in one dialog. */
export function AddStaffButton() {
  const [open, setOpen] = useState(false);
  const [created, setCreated] = useState<StaffCreated | null>(null);

  const change = (v: boolean) => {
    setOpen(v);
    if (!v) setCreated(null); // forget the temporary password as soon as the dialog closes
  };

  return (
    <FormDialog
      open={open}
      onOpenChange={change}
      title={created ? `${created.staff.fullName} added` : "Add staff member"}
      description={created ? `Staff number ${created.staff.staffNumber}` : "They get a login straight away; their email is the username."}
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          Add staff
        </Button>
      }
    >
      {(close) =>
        created ? (
          created.credentials ? (
            <CredentialSlips
              people={[{ fullName: created.staff.fullName, description: created.staff.jobTitle ?? "Staff", ...created.credentials }]}
              onDone={close}
            />
          ) : (
            <div className="space-y-4">
              <Alert tone="info" title="Linked to their existing account">
                {created.linkedExistingAccount
                  ? "This email already had a login (for example as a parent). They sign in with their current password."
                  : "No new login was issued."}
              </Alert>
              <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <Button variant="secondary" onClick={close}>
                  Done
                </Button>
                <Link href={`/admin/staff/${created.staff.id}`} className={buttonClasses("primary")} onClick={close}>
                  <UserRound className="size-4" aria-hidden />
                  View profile
                </Link>
              </div>
            </div>
          )
        ) : (
          <StaffForm mode="create" onCreated={setCreated} footer={<CancelButton />} />
        )
      }
    </FormDialog>
  );
}

