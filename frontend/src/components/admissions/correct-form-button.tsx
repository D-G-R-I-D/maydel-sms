"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { Pencil } from "lucide-react";
import type { ApplicationDetail } from "@/lib/admissions/form";
import { Button } from "@/components/ui/primitives";
import { FormDialog } from "@/components/ui/dialog";
import { ApplicationFormEditor } from "./application-form-editor";

/** Admin only: correct a submitted form (E11.5). Audited by the API; documents can't be changed here. */
export function CorrectFormButton({ detail }: { detail: ApplicationDetail }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  return (
    <FormDialog
      size="lg"
      title="Correct the application form"
      description={`${detail.application.referenceNumber} · changes are recorded in the audit log`}
      trigger={
        <Button variant="secondary" size="sm">
          <Pencil className="size-4" aria-hidden />
          Edit
        </Button>
      }
    >
      {(close) => (
        <ApplicationFormEditor
          detail={detail}
          mode="correction"
          onSaved={() => {
            close();
            startTransition(() => router.refresh());
          }}
        />
      )}
    </FormDialog>
  );
}
