import type { ApplicationDetail } from "@/lib/admissions/form";
import { Card } from "@/components/ui/primitives";

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
const day = (iso: string | null | undefined) => (iso ? dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`)) : null);

function Rows({ rows }: { rows: [string, string | null | undefined][] }) {
  return (
    <dl className="divide-y divide-border">
      {rows.map(([label, value]) => (
        <div key={label} className="grid grid-cols-[10rem_1fr] gap-3 py-2 text-sm">
          <dt className="text-muted">{label}</dt>
          <dd className="break-words whitespace-pre-line">{value || <span className="text-muted">—</span>}</dd>
        </div>
      ))}
    </dl>
  );
}

/** E11.5: a submitted application form, read-only (family after submitting; staff detail). */
export function ApplicationFormView({ detail }: { detail: ApplicationDetail }) {
  const a = detail.application;
  const f = detail.form;
  const block = (title: string, rows: [string, string | null | undefined][]) => (
    <Card className="p-5">
      <h2 className="mb-2 font-display text-lg font-semibold">{title}</h2>
      <Rows rows={rows} />
    </Card>
  );
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      {block("Child", [
        ["Name", [a.childFirstName, f.childMiddleName, a.childLastName].filter(Boolean).join(" ")],
        ["Date of birth", day(a.childDateOfBirth)],
        ["Sex", f.childSex],
        ["Religion", f.religion],
        ["State of origin", f.stateOfOrigin],
        ["LGA", f.lga],
        ["Home address", f.homeAddress],
        ["Previous school", f.previousSchool],
        ["Previous class", f.previousClass],
      ])}
      <div className="space-y-5">
        {block("Father / guardian", [
          ["Name", f.fatherName],
          ["Phone", f.fatherPhone],
          ["Email", f.fatherEmail],
          ["Occupation", f.fatherOccupation],
        ])}
        {block("Mother / guardian", [
          ["Name", f.motherName],
          ["Phone", f.motherPhone],
          ["Email", f.motherEmail],
          ["Occupation", f.motherOccupation],
        ])}
      </div>
      {block("Emergency contact", [
        ["Name", f.emergencyContactName],
        ["Phone", f.emergencyContactPhone],
      ])}
      {block("Medical", [
        ["Medical notes", f.medicalNotes],
        ["Special needs", f.specialNeeds],
        ["Declaration", f.declaration ? "Confirmed" : "Not confirmed"],
      ])}
    </div>
  );
}
