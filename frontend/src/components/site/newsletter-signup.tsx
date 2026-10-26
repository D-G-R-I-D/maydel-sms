"use client";

import { useState } from "react";
import { MailCheck, Send } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import { subscribe } from "@/lib/site/newsletter-actions";
import { Button, Input } from "@/components/ui/primitives";

/** Footer sign-up (E12.6). Double opt-in: the address gets a confirmation email first. */
export function NewsletterSignup() {
  const [email, setEmail] = useState("");
  const [website, setWebsite] = useState("");
  const [state, setState] = useState<"idle" | "busy" | "done" | string>("idle");
  const valid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());

  if (state === "done") {
    return (
      <p className="flex items-start gap-2 text-sm" role="status">
        <MailCheck className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
        Check your inbox and click the link to confirm your subscription.
      </p>
    );
  }

  return (
    <form
      className="space-y-2"
      onSubmit={async (e) => {
        e.preventDefault();
        if (!valid) return setState("Enter a valid email address.");
        setState("busy");
        try {
          const r = await subscribe(email, website);
          setState(r.ok ? "done" : r.problem.status === 429 ? "Too many tries. Please wait a minute." : (r.problem.errors?.email?.[0] ?? problemMessage(r.problem)));
        } catch {
          setState("Couldn't reach the school. Check your connection and try again.");
        }
      }}
    >
      <label htmlFor="footer-email" className="block text-sm font-medium">
        Get the school newsletter
      </label>
      <div className="flex gap-2">
        <Input id="footer-email" type="email" autoComplete="email" placeholder="you@example.com" value={email} onChange={(e) => setEmail(e.target.value)} className="min-w-0 flex-1" aria-describedby="footer-email-note" />
        <Button type="submit" loading={state === "busy"} loadingText="…" aria-label="Sign up">
          <Send className="size-4" aria-hidden />
        </Button>
      </div>
      {/* Honeypot: hidden from people, filled in by bots. */}
      <input type="text" name="website" tabIndex={-1} autoComplete="off" value={website} onChange={(e) => setWebsite(e.target.value)} className="hidden" aria-hidden />
      {state !== "idle" && state !== "busy" ? (
        <p className="text-xs text-danger" role="alert">
          {state}
        </p>
      ) : (
        <p id="footer-email-note" className="text-xs text-muted">
          We&apos;ll only send the school newsletter. Unsubscribe any time.
        </p>
      )}
    </form>
  );
}
