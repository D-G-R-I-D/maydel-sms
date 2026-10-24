"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { UserMinus } from "lucide-react";
import { runAction } from "@/lib/client/action";
import { unsubscribe } from "@/lib/website/actions";
import type { Subscriber } from "@/lib/website/cms";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Button } from "@/components/ui/primitives";

/** Takes an address off the newsletter list (e.g. when someone asks by phone). */
export function UnsubscribeButton({ subscriber }: { subscriber: Subscriber }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  return (
    <ConfirmDialog
      title="Unsubscribe this address?"
      confirmLabel="Unsubscribe"
      pendingLabel="Removing…"
      tone="destructive"
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Unsubscribe ${subscriber.email}`}>
          <UserMinus className="size-4" aria-hidden />
          <span className="hidden sm:inline">Unsubscribe</span>
        </Button>
      }
      onConfirm={async () => {
        const r = await runAction(() => unsubscribe(subscriber.id), { success: `${subscriber.email} won't get the newsletter` });
        if (r) startTransition(() => router.refresh());
        return Boolean(r);
      }}
    >
      {subscriber.email} will stop getting the school newsletter. They can sign up again from the website.
    </ConfirmDialog>
  );
}
