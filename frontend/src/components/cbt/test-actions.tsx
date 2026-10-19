"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Pencil, Trash2 } from "lucide-react";
import { deleteTest } from "@/lib/cbt/actions";
import { testsHref, type ComponentOption, type TestDetail } from "@/lib/cbt/tests";
import type { AuthoringScope } from "@/lib/cbt/questions";
import { runAction } from "@/lib/client/action";
import type { TermOption } from "@/components/assessment/term-picker";
import { Button } from "@/components/ui/primitives";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { TestHeaderForm, type ArmOption } from "./test-header-form";

/** Draft-only header actions: edit the details (header step) and delete. */
export function TestActions({ test, scopes, terms, components, arms }: { test: TestDetail; scopes: AuthoringScope[]; terms: TermOption[]; components: ComponentOption[]; arms: ArmOption[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [confirmDelete, setConfirmDelete] = useState(false);

  return (
    <div className="flex flex-wrap gap-2">
      <FormDialog
        title="Edit details"
        size="lg"
        trigger={
          <Button size="sm" variant="secondary">
            <Pencil className="size-4" aria-hidden />
            Edit details
          </Button>
        }
      >
        {(close) => (
          <TestHeaderForm
            test={test}
            termId={test.termId}
            scopes={scopes}
            terms={terms}
            components={components}
            arms={arms}
            onSaved={() => {
              close();
              startTransition(() => router.refresh());
            }}
          />
        )}
      </FormDialog>
      <Button size="sm" variant="ghost" onClick={() => setConfirmDelete(true)}>
        <Trash2 className="size-4" aria-hidden />
        Delete
      </Button>
      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title={`Delete “${test.title}”?`}
        confirmLabel="Delete test"
        pendingLabel="Deleting…"
        tone="destructive"
        onConfirm={async () => {
          const r = await runAction(() => deleteTest(test.id), { success: "Test deleted" });
          if (r) router.push(testsHref(test.termId));
          return r !== null;
        }}
      >
        <p>The draft and its sections are removed. The questions stay in the bank.</p>
      </ConfirmDialog>
    </div>
  );
}
