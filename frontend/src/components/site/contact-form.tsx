"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CheckCircle2, Send } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { sendContactMessage } from "@/lib/site/actions";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";

const SUBJECTS = ["Admission enquiry", "General question", "Fees and payments", "Feedback", "Other"] as const;
import { Alert } from "@/components/ui/states";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

// Mirrors SendContactMessageHandler.
const schema = z.object({
  name: z.string().trim().min(1, "Enter your name.").max(120, "Use 120 characters or fewer."),
  email: z.string().trim().min(1, "Enter your email so we can reply.").max(254).email("Enter a valid email address so we can reply."),
  phone: z.string().trim().max(30, "Enter a valid phone number."),
  subject: z.string().trim().max(150, "Keep the subject under 150 characters."),
  message: z.string().trim().min(5, "Write your message.").max(4000, "Keep it under 4000 characters."),
  website: z.string(),
});
type Values = z.input<typeof schema>;
const fields = ["name", "email", "phone", "subject", "message"] as const;

/** E7: send the school a message. It lands in the console inbox and is emailed to the school address. */
export function ContactForm() {
  const [sent, setSent] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { name: "", email: "", phone: "", subject: "", message: "", website: "" } });

  const onSubmit = handleSubmit(async (v) => {
    setProblem(null);
    const r = await sendContactMessage({ ...v, phone: v.phone.trim() || null, subject: v.subject.trim() || null }).catch(() => ({ ok: false as const, problem: unreachableProblem() }));
    if (r.ok) {
      setSent(true);
      reset();
      return;
    }
    const p = r.problem;
    if (p.status === 400 && p.errors) {
      for (const [k, msgs] of Object.entries(p.errors)) if ((fields as readonly string[]).includes(k)) setError(k as (typeof fields)[number], { type: "server", message: msgs.join(" ") });
      return;
    }
    setProblem(p.status === 429 ? { ...p, title: "Too many messages from here. Please wait a few minutes and try again." } : p);
  });

  if (sent) {
    return (
      <Alert tone="success" title="Thank you, your message is on its way">
        <span className="flex flex-col gap-2">
          <span>The school office will reply by email, usually within one working day.</span>
          <button type="button" className="self-start font-medium text-brand hover:underline" onClick={() => setSent(false)}>
            <CheckCircle2 className="mr-1 inline size-4" aria-hidden />
            Send another message
          </button>
        </span>
      </Alert>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {problem && (
        <Alert tone="danger" title="Couldn't send your message">
          {problemMessage(problem)}
        </Alert>
      )}
      {/* Honeypot: hidden from people and assistive tech; bots fill it in. */}
      <div className="absolute -left-[9999px] h-px w-px overflow-hidden" aria-hidden="true">
        <label htmlFor="contact-website">Website (leave empty)</label>
        <input id="contact-website" type="text" tabIndex={-1} autoComplete="off" {...register("website")} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="contact-name" label="Your name" error={errors.name?.message}>
          <Input {...describedBy("contact-name", errors.name?.message)} {...register("name")} autoComplete="name" />
        </Field>
        <Field id="contact-email" label="Email" error={errors.email?.message}>
          <Input {...describedBy("contact-email", errors.email?.message)} {...register("email")} type="email" autoComplete="email" spellCheck={false} />
        </Field>
        <Field id="contact-phone" label="Phone (optional)" error={errors.phone?.message}>
          <Input {...describedBy("contact-phone", errors.phone?.message)} {...register("phone")} type="tel" inputMode="tel" autoComplete="tel" />
        </Field>
        <Field id="contact-subject" label="Subject" error={errors.subject?.message}>
          <Select {...describedBy("contact-subject", errors.subject?.message)} {...register("subject")}>
            <option value="">Choose a subject…</option>
            {SUBJECTS.map((x) => (
              <option key={x} value={x}>
                {x}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <Field id="contact-message" label="Message" error={errors.message?.message}>
        <textarea {...describedBy("contact-message", errors.message?.message)} {...register("message")} rows={6} className={textareaClass} />
      </Field>
      <Button type="submit" loading={isSubmitting} loadingText="Sending…">
        <Send className="size-4" aria-hidden />
        Send message
      </Button>
    </form>
  );
}
