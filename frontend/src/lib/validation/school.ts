import { z } from "zod";

/** Client mirror of the E2.6 school profile rules (docs/handoffs/e2.6-school-profile.md). The API re-checks. */

const optionalText = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Use ${max} characters or fewer.`)
    .transform((v) => (v === "" ? null : v));

/** Optional https:// link (the API rejects anything else). */
const httpsLink = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Use ${max} characters or fewer.`)
    .refine((v) => v === "" || /^https:\/\/[^\s/$.?#].[^\s]*$/i.test(v), "Enter a full https:// link.")
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
  // Website (E7): the footer, contact page and WhatsApp button read these.
  whatsApp: z
    .string()
    .trim()
    .max(40, "Use 40 characters or fewer.")
    .refine((v) => v === "" || /^\+?[\d\s()-]{7,20}$/.test(v), "Enter a valid WhatsApp number, e.g. 0803 123 4567.")
    .transform((v) => (v === "" ? null : v)),
  mapUrl: httpsLink(500),
  facebookUrl: httpsLink(300),
  instagramUrl: httpsLink(300),
  officeHours: optionalText(120),
});
export type SchoolProfileFormValues = z.input<typeof schoolProfileSchema>;
export type SchoolProfilePayload = z.output<typeof schoolProfileSchema>;
