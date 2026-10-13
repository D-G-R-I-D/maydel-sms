import { z } from "zod";

/** Mirrors the API's password policy (handoff "Password rules"); the server re-checks. */
export const newPasswordRules = z
  .string()
  .min(10, "Use at least 10 characters.")
  .max(128, "Use 128 characters or fewer.")
  .regex(/[A-Z]/, "Include an upper-case letter.")
  .regex(/[a-z]/, "Include a lower-case letter.")
  .regex(/[0-9]/, "Include a number.");

export const loginSchema = z.object({
  userName: z.string().trim().min(1, "Enter your email or admission number."),
  password: z.string().min(1, "Enter your password."),
});
export type LoginInput = z.infer<typeof loginSchema>;

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "Enter your current password."),
    newPassword: newPasswordRules,
    confirmPassword: z.string().min(1, "Type the new password again."),
  })
  .refine((v) => v.newPassword !== v.currentPassword, {
    path: ["newPassword"],
    message: "Choose a password different from your current one.",
  })
  .refine((v) => v.confirmPassword === v.newPassword, {
    path: ["confirmPassword"],
    message: "The passwords don't match.",
  });
export type ChangePasswordInput = z.infer<typeof changePasswordSchema>;

/**
 * A rough 0–4 strength score for the meter: 0 empty, 1 weak … 4 strong. Guidance only; the
 * policy above is what's enforced. Rewards length and variety, penalises repeats and "password".
 */
export function passwordStrength(password: string): { score: 0 | 1 | 2 | 3 | 4; label: string } {
  if (!password) return { score: 0, label: "" };
  let points = 0;
  if (password.length >= 10) points++;
  if (password.length >= 14) points++;
  const classes = [/[a-z]/, /[A-Z]/, /[0-9]/, /[^A-Za-z0-9]/].filter((r) => r.test(password)).length;
  points += Math.max(0, classes - 2);
  if (/(.)\1{2,}/.test(password) || /password|maydel|123456|qwerty/i.test(password)) points--;
  const meetsPolicy = newPasswordRules.safeParse(password).success;
  const score = (meetsPolicy ? Math.min(4, Math.max(2, points + 1)) : Math.min(1, Math.max(1, points))) as 1 | 2 | 3 | 4;
  return { score, label: ["", "Weak", "Fair", "Good", "Strong"][score] };
}

/** Checklist shown under the new-password field. */
export const passwordChecklist: { label: string; test: (v: string) => boolean }[] = [
  { label: "At least 10 characters", test: (v) => v.length >= 10 },
  { label: "An upper-case letter", test: (v) => /[A-Z]/.test(v) },
  { label: "A lower-case letter", test: (v) => /[a-z]/.test(v) },
  { label: "A number", test: (v) => /[0-9]/.test(v) },
];
