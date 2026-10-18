import { describe, expect, it } from "vitest";
import { applicationFormSchema, documentFileError } from "./form";

const blankForm = {
  childMiddleName: "",
  childSex: "" as const,
  stateOfOrigin: "",
  lga: "",
  religion: "",
  homeAddress: "",
  previousSchool: "",
  previousClass: "",
  medicalNotes: "",
  specialNeeds: "",
  fatherName: "",
  fatherPhone: "",
  fatherEmail: "",
  fatherOccupation: "",
  motherName: "",
  motherPhone: "",
  motherEmail: "",
  motherOccupation: "",
  emergencyContactName: "",
  emergencyContactPhone: "",
  declaration: false,
};

describe("application form", () => {
  it("saves a draft with blanks as null", () => {
    const r = applicationFormSchema.parse({ childFirstName: " Ada ", childLastName: "Eze", childDateOfBirth: "", form: { ...blankForm, homeAddress: " 12 Road " } });
    expect(r.childFirstName).toBe("Ada");
    expect(r.childDateOfBirth).toBeNull();
    expect(r.form.homeAddress).toBe("12 Road");
    expect(r.form.childSex).toBeNull();
    expect(r.form.fatherEmail).toBeNull();
  });

  it("mirrors the API's limits", () => {
    const bad = applicationFormSchema.safeParse({ childFirstName: "Ada", childLastName: "Eze", childDateOfBirth: "", form: { ...blankForm, fatherEmail: "nope", fatherPhone: "1".repeat(31) } });
    expect(bad.success).toBe(false);
    expect(bad.error?.issues.map((i) => i.path.join("."))).toEqual(["form.fatherPhone", "form.fatherEmail"]);
  });
});

describe("documents", () => {
  it("passport photo must be a photo; PDFs ≤ 5 MB", () => {
    expect(documentFileError({ type: "application/pdf", size: 1000, name: "p.pdf" }, "PassportPhoto")).toMatch(/must be a photo/);
    expect(documentFileError({ type: "image/jpeg", size: 1000, name: "p.jpg" }, "PassportPhoto")).toBeNull();
    expect(documentFileError({ type: "application/pdf", size: 6 * 1024 * 1024, name: "b.pdf" }, "BirthCertificate")).toBe("PDFs must be 5 MB or smaller.");
    expect(documentFileError({ type: "text/plain", size: 10, name: "x.txt" }, "Other")).toMatch(/photo .* or a PDF/);
  });
});
