"use client";

import { useState } from "react";
import { CheckCircle2, MailX, MailCheck } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import { confirmSubscription, unsubscribeNewsletter } from "@/lib/site/newsletter-actions";
import { markSubscribed } from "@/lib/site/newsletter-prefs";
import { Button, Card } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

/**
 * Confirm or unsubscribe from an email link. Only on a button press: mail scanners open links in
 * emails, so doing it on page load would confirm or unsubscribe people by accident.
 */
export function NewsletterLinkAction({ kind, token }: { kind: "confirm" | "unsubscribe"; token: string }) {
  const [state, setState] = useState<"idle" | "busy" | "done" | string>("idle");
  const confirm = kind === "confirm";

  if (state === "done") {
    return (
      <Card className="flex items-start gap-3 p-6" role="status">
        <CheckCircle2 className="size-7 shrink-0 text-success" aria-hidden />
        <div>
          <p className="font-display text-xl font-semibold">{confirm ? "You're subscribed" : "You've been unsubscribed"}</p>
          <p className="mt-1 text-muted">
            {confirm ? "Thank you. The next school newsletter will come to your inbox." : "You won't get the newsletter any more. Emails about fees and results still arrive."}
          </p>
        </div>
      </Card>
    );
  }

  return (
    <Card className="space-y-4 p-6">
      {confirm ? <MailCheck className="size-8 text-brand" aria-hidden /> : <MailX className="size-8 text-brand" aria-hidden />}
      <p className="text-lg">{confirm ? "Confirm that you'd like the Maydel Spring Schools newsletter." : "Stop getting the Maydel Spring Schools newsletter?"}</p>
      {state !== "idle" && state !== "busy" && <Alert tone="danger">{state}</Alert>}
      <Button
        size="lg"
        loading={state === "busy"}
        loadingText={confirm ? "Confirming…" : "Unsubscribing…"}
        disabled={!token}
        onClick={async () => {
          setState("busy");
          try {
            const r = await (confirm ? confirmSubscription(token) : unsubscribeNewsletter(token));
            if (r.ok && confirm) markSubscribed();
            setState(r.ok ? "done" : r.problem.status === 404 ? "This link has expired or was already used." : problemMessage(r.problem));
          } catch {
            setState("Couldn't reach the school. Check your connection and try again.");
          }
        }}
      >
        {confirm ? "Confirm my subscription" : "Unsubscribe"}
      </Button>
    </Card>
  );
}
