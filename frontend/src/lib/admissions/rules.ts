import { z } from "zod";
import type { ApplicationStatus } from "@/lib/api/types";
import { newPasswordRules } from "@/lib/validation/auth";

/** E11 part 1: admission form purchase. Client mirrors of the API's rules; the API re-checks. */

const phone = z
  .string()
  .trim()
  .min(1, "Enter a phone number.")
  .refine((v) => /^\+?[0-9 ()-]{7,32}$/.test(v), "Use digits, spaces, brackets or dashes, e.g. +234 803 123 4567.");
const name = (what: string, max = 100) => z.string().trim().min(1, `Enter the ${what}.`).max(max, `Use ${max} characters or fewer.`);
const dateOfBirth = z
  .string()
  .refine((v) => v === "" || /^\d{4}-\d{2}-\d{2}$/.test(v), "Use the date picker.")
  .transform((v) => (v === "" ? null : v));

/** The child being applied for (also "Apply for another child"). */
export const childSchema = z.object({
  childFirstName: name("child's first name"),
  childLastName: name("child's surname"),
  childDateOfBirth: dateOfBirth,
  classLevelId: z.string().min(1, "Choose the class."),
  sessionId: z.string().min(1, "Choose the session."),
});
export type ChildFormValues = z.input<typeof childSchema>;
export type ChildPayload = z.output<typeof childSchema>;

/** Buy a form: the parent's account details plus the first child. */
export const registerSchema = childSchema.extend({
  parentName: name("parent's full name", 150),
  email: z.string().trim().min(1, "Enter an email address.").max(256, "Use 256 characters or fewer.").email("Enter a valid email address."),
  phone,
  password: newPasswordRules,
  /** Honeypot: hidden from people; bots fill it in. */
  website: z.string(),
});
export type RegisterFormValues = z.input<typeof registerSchema>;
export type RegisterPayload = z.output<typeof registerSchema>;

/** Form codes look like ABCD-EFGH (no 0/O or 1/I/L); accept any case and spacing, as the API does. */
export function normalizeFormCode(input: string): string {
  const raw = input.toUpperCase().replace(/[^A-Z0-9]/g, "");
  return raw.length === 8 ? `${raw.slice(0, 4)}-${raw.slice(4)}` : raw;
}

export function formCodeError(input: string): string | null {
  const code = normalizeFormCode(input);
  if (!code) return "Enter the form code from your receipt.";
  return /^[A-HJ-KM-NP-Z2-9]{4}-[A-HJ-KM-NP-Z2-9]{4}$/.test(code) ? null : "Form codes look like ABCD-EFGH. Check the code on your receipt.";
}

export const applicationStatusLabel: Record<ApplicationStatus, string> = {
  AwaitingPayment: "Form not paid yet",
  FormPaid: "Form paid",
  Submitted: "Form submitted",
  ExamBooked: "Exam booked",
  Missed: "Missed exam",
  Assessed: "Assessed",
  Offered: "Offered a place",
  NotOffered: "Not offered",
  Accepted: "Accepted",
  Enrolled: "Enrolled",
  Withdrawn: "Withdrawn",
};

export const applicationStatusTone: Record<ApplicationStatus, "neutral" | "warning" | "success" | "danger" | "brand"> = {
  AwaitingPayment: "warning",
  FormPaid: "brand",
  Submitted: "brand",
  ExamBooked: "brand",
  Missed: "danger",
  Assessed: "neutral",
  Offered: "success",
  NotOffered: "neutral",
  Accepted: "success",
  Enrolled: "success",
  Withdrawn: "neutral",
};
