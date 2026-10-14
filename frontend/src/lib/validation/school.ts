import { z } from "zod";

/** Client mirror of the E2.6 school profile rules (docs/handoffs/e2.6-school-profile.md). The API re-checks. */

const optionalText = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Use ${max} characters or fewer.`)
    .transform((v) => (v === "" ? null : v));

export const schoolProfileSchema = z.object({
  name: z.string().trim().min(1, "Enter the school's name.").max(120, "Use 120 characters or fewer."),
  motto: optionalText(120),
  address: z.string().trim().min(1, "Enter the school's address.").max(300, "Use 300 characters or fewer."),
  phone: optionalText(40),
  email: z
    .string()
    .trim()
    .max(120, "Use 120 characters or fewer.")
    .refine((v) => v === "" || z.string().email().safeParse(v).success, "Enter a valid email address.")
    .transform((v) => (v === "" ? null : v)),
  website: optionalText(120),
  principalName: optionalText(80),
});
export type SchoolProfileFormValues = z.input<typeof schoolProfileSchema>;
export type SchoolProfilePayload = z.output<typeof schoolProfileSchema>;
