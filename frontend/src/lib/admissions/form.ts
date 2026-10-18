import { z } from "zod";
import type { components } from "@/lib/api/schema";

/** E11.5: the online application form. Mirrors SaveApplicationFormValidator; Submit's checks come from the API. */

export type ApplicationDetail = components["schemas"]["ApplicationDetail"];
export type ApplicationForm = components["schemas"]["ApplicationForm"];
export type DocumentKind = components["schemas"]["DocumentKind"];

const text = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Use ${max} characters or fewer.`)
    .transform((v) => (v === "" ? null : v));
const email = z
  .string()
  .trim()
  .max(256, "Use 256 characters or fewer.")
  .refine((v) => v === "" || z.string().email().safeParse(v).success, "Enter a valid email address.")
  .transform((v) => (v === "" ? null : v));

export const applicationFormSchema = z.object({
  childFirstName: z.string().trim().min(1, "Enter the child's first name.").max(60, "Use 60 characters or fewer."),
  childLastName: z.string().trim().min(1, "Enter the child's surname.").max(60, "Use 60 characters or fewer."),
  childDateOfBirth: z.string().transform((v) => (v === "" ? null : v)),
  form: z.object({
    childMiddleName: text(120),
    childSex: z.enum(["", "Male", "Female"]).transform((v) => (v === "" ? null : v)),
    stateOfOrigin: text(120),
    lga: text(120),
    religion: text(120),
    homeAddress: text(300),
    previousSchool: text(120),
    previousClass: text(120),
    medicalNotes: text(1000),
    specialNeeds: text(1000),
    fatherName: text(120),
    fatherPhone: text(30),
    fatherEmail: email,
    fatherOccupation: text(120),
    motherName: text(120),
    motherPhone: text(30),
    motherEmail: email,
    motherOccupation: text(120),
    emergencyContactName: text(120),
    emergencyContactPhone: text(30),
    declaration: z.boolean(),
  }),
});
export type ApplicationFormValues = z.input<typeof applicationFormSchema>;
export type ApplicationFormPayload = z.output<typeof applicationFormSchema>;

const s = (v: string | null | undefined) => v ?? "";

export function formValues(detail: ApplicationDetail): ApplicationFormValues {
  const f = detail.form;
  return {
    childFirstName: detail.application.childFirstName,
    childLastName: detail.application.childLastName,
    childDateOfBirth: s(detail.application.childDateOfBirth),
    form: {
      childMiddleName: s(f.childMiddleName),
      childSex: (f.childSex ?? "") as "" | "Male" | "Female",
      stateOfOrigin: s(f.stateOfOrigin),
      lga: s(f.lga),
      religion: s(f.religion),
      homeAddress: s(f.homeAddress),
      previousSchool: s(f.previousSchool),
      previousClass: s(f.previousClass),
      medicalNotes: s(f.medicalNotes),
      specialNeeds: s(f.specialNeeds),
      fatherName: s(f.fatherName),
      fatherPhone: s(f.fatherPhone),
      fatherEmail: s(f.fatherEmail),
      fatherOccupation: s(f.fatherOccupation),
      motherName: s(f.motherName),
      motherPhone: s(f.motherPhone),
      motherEmail: s(f.motherEmail),
      motherOccupation: s(f.motherOccupation),
      emergencyContactName: s(f.emergencyContactName),
      emergencyContactPhone: s(f.emergencyContactPhone),
      declaration: f.declaration ?? false,
    },
  };
}

/** What `missingToSubmit` keys mean, for the "still needed" checklist. */
export const missingLabel: Record<string, string> = {
  childDateOfBirth: "The child's date of birth",
  childSex: "The child's sex",
  homeAddress: "The home address",
  fatherName: "At least one parent's name and phone number",
  motherName: "At least one parent's name and phone number",
  declaration: "Tick the declaration",
  documents: "A passport photograph",
};

export const documentKindLabel: Record<DocumentKind, string> = {
  PassportPhoto: "Passport photograph",
  BirthCertificate: "Birth certificate",
  LastReportCard: "Last report card",
  Other: "Other document",
};

export const MAX_DOCUMENTS = 6;
export const DOC_ACCEPT = "image/jpeg,image/png,image/webp,application/pdf";

/** Photos ≤ 12 MB (re-encoded by the API), PDFs ≤ 5 MB; the passport photo must be a photo. */
export function documentFileError(file: { type: string; size: number; name: string }, kind: DocumentKind): string | null {
  if (/\.(heic|heif)$/i.test(file.name) || /heic|heif/i.test(file.type)) return "iPhone HEIC photos aren't accepted. Share it as JPEG (choose “Most Compatible”).";
  if (file.size === 0) return "That file is empty.";
  const image = ["image/jpeg", "image/png", "image/webp"].includes(file.type);
  if (kind === "PassportPhoto" && !image) return "The passport photograph must be a photo (JPEG, PNG or WebP), not a PDF.";
  if (file.type === "application/pdf") return file.size > 5 * 1024 * 1024 ? "PDFs must be 5 MB or smaller." : null;
  if (image) return file.size > 12 * 1024 * 1024 ? "Photos must be 12 MB or smaller." : null;
  return "Upload a photo (JPEG, PNG or WebP) or a PDF.";
}
