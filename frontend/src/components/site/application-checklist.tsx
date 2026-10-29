import { CheckCircle2, ClipboardList } from "lucide-react";
import { applicationChecklist, applicationFormSections } from "@/lib/site/content";
import { Card, cn } from "@/components/ui/primitives";

/** "What you'll need": the documents to have ready and the form's sections, shown before paying for the form. */
export function ApplicationChecklist({ className, compact = false }: { className?: string; compact?: boolean }) {
  return (
    <Card className={cn("space-y-4 p-5", className)}>
      <h2 className="flex items-center gap-2 font-display text-lg font-semibold">
        <ClipboardList className="size-5 text-brand" aria-hidden />
        What you&apos;ll need
      </h2>
      <ul className="space-y-2 text-sm">
        {applicationChecklist.map((c) => (
          <li key={c.item} className="flex gap-2">
            <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
            <span>
              <span className="font-medium">{c.item}</span>
              {!compact && <span className="block text-muted">{c.note}</span>}
            </span>
          </li>
        ))}
      </ul>
      <div className="border-t border-border pt-3 text-sm">
        <p className="font-medium">The form has {applicationFormSections.length} short sections</p>
        <p className="mt-1 text-muted">{applicationFormSections.join(" · ")}</p>
        <p className="mt-2 text-muted">The form opens online in your family account once the form fee is paid.</p>
      </div>
    </Card>
  );
}
