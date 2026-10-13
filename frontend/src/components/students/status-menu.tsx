"use client";

import * as Menu from "@radix-ui/react-dropdown-menu";
import { useState } from "react";
import { ChevronDown } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import type { StudentStatus } from "@/lib/api/types";
import { changeStudentStatus } from "@/lib/students/actions";
import { runAction } from "@/lib/client/action";
import { nextStatuses, statusActionLabel, statusConsequence } from "@/lib/validation/students";
import { Button } from "@/components/ui/primitives";
import { ConfirmDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

/** "Change status" menu offering only allowed transitions; each one is confirmed first. */
export function StatusMenu({ studentId, fullName, status }: { studentId: string; fullName: string; status: StudentStatus }) {
  const options = nextStatuses(status);
  const [target, setTarget] = useState<StudentStatus | null>(null);
  if (options.length === 0) return null;

  return (
    <>
      <Menu.Root>
        <Menu.Trigger asChild>
          <Button variant="secondary">
            Change status
            <ChevronDown className="size-4" aria-hidden />
          </Button>
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Content align="end" sideOffset={6} className="z-50 w-60 rounded-card border border-border bg-surface p-1.5 text-text shadow-low">
            {options.map((s) => (
              <Menu.Item
                key={s}
                onSelect={() => setTarget(s)}
                className="flex min-h-10 cursor-pointer items-center rounded-input px-3 text-sm outline-none data-[highlighted]:bg-surface-2"
              >
                {statusActionLabel[s]}
              </Menu.Item>
            ))}
          </Menu.Content>
        </Menu.Portal>
      </Menu.Root>
      <ConfirmDialog
        open={target !== null}
        onOpenChange={(v) => !v && setTarget(null)}
        title={target ? `${statusActionLabel[target]}: ${fullName}?` : ""}
        confirmLabel={target ? statusActionLabel[target] : "Confirm"}
        tone={target === "Active" ? "primary" : "destructive"}
        onConfirm={async () => {
          if (!target) return true;
          const saved = await runAction(() => changeStudentStatus(studentId, target), {
            success: (s) => `${s.fullName} is now ${s.status.toLowerCase()}`,
            onProblem: (p) => {
              if (p.status > 0 && p.status < 500 && p.status !== 429) toast.error("Status not changed", { description: problemMessage(p) });
            },
          });
          return saved !== null;
        }}
      >
        {target && <p>{statusConsequence(status, target)}</p>}
      </ConfirmDialog>
    </>
  );
}
