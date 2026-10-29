"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { XCircle } from "lucide-react";
import { runAction } from "@/lib/client/action";
import { cancelShopOrder } from "@/lib/store/actions";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Button } from "@/components/ui/primitives";

/** A parent can cancel while the order is still New (before the office packs it). */
export function CancelShopOrder({ studentId, orderId, number }: { studentId: string; orderId: string; number: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  return (
    <ConfirmDialog
      title={`Cancel order ${number}?`}
      confirmLabel="Cancel order"
      cancelLabel="Keep it"
      pendingLabel="Cancelling…"
      tone="destructive"
      onConfirm={async () => {
        const r = await runAction(() => cancelShopOrder(studentId, orderId), { success: "Order cancelled" });
        if (r) startTransition(() => router.refresh());
        return Boolean(r);
      }}
      trigger={
        <Button variant="ghost" size="sm">
          <XCircle className="size-4" aria-hidden />
          Cancel order
        </Button>
      }
    >
      The items go back on the shelf. You can order again any time.
    </ConfirmDialog>
  );
}
