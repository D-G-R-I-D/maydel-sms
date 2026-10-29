"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Archive, Check, Mail, MailOpen } from "lucide-react";
import { runAction } from "@/lib/client/action";
import { setMessageStatus } from "@/lib/website/actions";
import { replyHref, type ContactMessage, type MessageStatus } from "@/lib/website/cms";
import { Button, buttonClasses } from "@/components/ui/primitives";

/** Reply by email and move a message along: New → Read → Replied, or Archived. */
export function MessageActions({ message }: { message: ContactMessage }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [busy, setBusy] = useState<MessageStatus | null>(null);

  const set = async (status: MessageStatus, success: string) => {
    setBusy(status);
    const r = await runAction(() => setMessageStatus(message.id, status), { success });
    setBusy(null);
    if (r) startTransition(() => router.refresh());
  };

  return (
    <div className="flex flex-wrap gap-2">
      <a href={replyHref(message)} className={buttonClasses("primary", "sm")} onClick={() => message.status === "New" && void set("Read", "Marked as read")}>
        <Mail className="size-4" aria-hidden />
        Reply by email
      </a>
      {message.status !== "Replied" && (
        <Button size="sm" variant="secondary" loading={busy === "Replied"} loadingText="Saving…" onClick={() => set("Replied", "Marked as replied")}>
          <Check className="size-4" aria-hidden />
          Mark replied
        </Button>
      )}
      {message.status === "New" && (
        <Button size="sm" variant="ghost" loading={busy === "Read"} loadingText="Saving…" onClick={() => set("Read", "Marked as read")}>
          <MailOpen className="size-4" aria-hidden />
          Mark read
        </Button>
      )}
      {message.status === "Archived" ? (
        <Button size="sm" variant="ghost" loading={busy === "Read"} loadingText="Saving…" onClick={() => set("Read", "Moved back to the inbox")}>
          <MailOpen className="size-4" aria-hidden />
          Unarchive
        </Button>
      ) : (
        <Button size="sm" variant="ghost" loading={busy === "Archived"} loadingText="Saving…" onClick={() => set("Archived", "Archived")}>
          <Archive className="size-4" aria-hidden />
          Archive
        </Button>
      )}
    </div>
  );
}
