"use client";

import Image from "next/image";
import { useState } from "react";
import { createPortal } from "react-dom";
import { Check, Copy, Printer, ShieldAlert } from "lucide-react";
import { Button, cn } from "@/components/ui/primitives";
import { toast } from "@/components/ui/toast";

export type SlipPerson = {
  fullName: string;
  /** e.g. "Staff", "Student · Year 3 A", "Parent of Chidi Eze" */
  description?: string;
  userName: string;
  temporaryPassword: string;
};

/**
 * One printable sign-in slip. Reused for staff now and students/parents in part 3.
 * The temporary password exists only in this component's props: never persisted or logged.
 */
export function CredentialSlip({ person, signInUrl }: { person: SlipPerson; signInUrl: string }) {
  return (
    <article className="credential-slip rounded-card border-2 border-dashed border-border bg-surface p-5 text-text print:rounded-none print:border-gray-400">
      <header className="flex items-center gap-3 border-b border-border pb-3">
        <Image src="/brand/crest.svg" alt="" width={36} height={45} />
        <div>
          <p className="font-display text-lg font-semibold leading-tight">Maydel Spring Schools</p>
          <p className="text-xs text-muted">Sign-in details</p>
        </div>
      </header>
      <dl className="mt-4 space-y-3">
        <div>
          <dt className="text-xs uppercase tracking-wider text-muted">Name</dt>
          <dd className="font-medium">{person.fullName}</dd>
          {person.description && <dd className="text-sm text-muted">{person.description}</dd>}
        </div>
        <div>
          <dt className="text-xs uppercase tracking-wider text-muted">Username</dt>
          <dd className="break-all font-mono text-base">{person.userName}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase tracking-wider text-muted">Temporary password</dt>
          <dd className="mt-1 rounded-input bg-surface-2 px-3 py-2 font-mono text-2xl font-semibold tracking-wider break-all select-all print:bg-transparent print:px-0">
            {person.temporaryPassword}
          </dd>
        </div>
        <div>
          <dt className="text-xs uppercase tracking-wider text-muted">Sign in at</dt>
          <dd className="break-all font-mono text-sm">{signInUrl}</dd>
        </div>
      </dl>
      <p className="mt-4 border-t border-border pt-3 text-sm">You&apos;ll be asked to choose a new password the first time you sign in.</p>
    </article>
  );
}

/** Slip(s) plus the one-time warning, Copy and Print. Print shows only the slips, one per page. */
export function CredentialSlips({ people, onDone, doneLabel = "Done" }: { people: SlipPerson[]; onDone?: () => void; doneLabel?: string }) {
  // Slips only render after a client-side action, so window is available here.
  const [signInUrl] = useState(() => (typeof window === "undefined" ? "/login" : `${window.location.origin}/login`));
  const [copied, setCopied] = useState<string | null>(null);

  const copy = async (p: SlipPerson) => {
    try {
      await navigator.clipboard.writeText(`${p.userName}\n${p.temporaryPassword}`);
      setCopied(p.userName);
      toast.success("Copied username and password");
    } catch {
      toast.error("Couldn't copy", { description: "Your browser blocked clipboard access." });
    }
  };

  return (
    <div className="space-y-4">
      <div role="alert" className="flex gap-3 rounded-input border border-warning/40 bg-warning/8 p-3 text-sm print:hidden">
        <ShieldAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
        <p>
          <span className="font-medium">This password won&apos;t be shown again.</span> Print the slip or copy it now and hand it over in person.
        </p>
      </div>
      <div className="space-y-4">
        {people.map((p) => (
          <div key={p.userName} className="space-y-2">
            <CredentialSlip person={p} signInUrl={signInUrl} />
            <Button variant="ghost" size="sm" className="print:hidden" onClick={() => copy(p)}>
              {copied === p.userName ? <Check className="size-4" aria-hidden /> : <Copy className="size-4" aria-hidden />}
              Copy username and password
            </Button>
          </div>
        ))}
      </div>
      <div className={cn("flex flex-col-reverse gap-2 sm:flex-row sm:justify-end print:hidden")}>
        {onDone && (
          <Button variant="secondary" onClick={onDone}>
            {doneLabel}
          </Button>
        )}
        <Button onClick={() => window.print()}>
          <Printer className="size-4" aria-hidden />
          Print slip{people.length > 1 ? "s" : ""}
        </Button>
      </div>
      {/* Print-only copy at <body> level, so print CSS can hide the rest of the page (and the dialog). */}
      {typeof document !== "undefined" &&
        createPortal(
          <div className="print-root hidden">
            {people.map((p) => (
              <div key={p.userName} className="break-after-page last:break-after-auto">
                <CredentialSlip person={p} signInUrl={signInUrl} />
              </div>
            ))}
          </div>,
          document.body,
        )}
    </div>
  );
}
