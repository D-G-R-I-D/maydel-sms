import { AlertTriangle, CheckCircle2 } from "lucide-react";
import { watTime } from "@/lib/clock/clock";
import type { Check } from "@/lib/transport/transport";
import { cn } from "@/components/ui/primitives";

/** A submitted checklist: who, when, each answer (flagged ones highlighted) and the driver's notes. */
export function CheckAnswers({ check }: { check: Check }) {
  return (
    <div className="space-y-2 text-sm">
      <p className="flex items-center gap-2">
        {check.isFlagged ? <AlertTriangle className="size-4 text-warning" aria-hidden /> : <CheckCircle2 className="size-4 text-success" aria-hidden />}
        <span>
          {check.driverName} at {watTime(check.submittedAt)}
          {check.isFlagged && <span className="ml-1 font-medium text-warning">· flagged</span>}
        </span>
      </p>
      <ul className="space-y-1">
        {check.answers.map((a) => (
          <li key={a.questionId} className={cn("flex gap-2 rounded-input px-2 py-1", a.flagged && "bg-warning/10")}>
            <span className="min-w-0 flex-1 text-muted">{a.question}</span>
            <span className={cn("shrink-0 font-medium", a.flagged && "text-warning")}>
              {a.kind === "YesNo" ? (a.yes == null ? "—" : a.yes ? "Yes" : "No") : a.kind === "Number" ? (a.number ?? "—") : a.text || "—"}
            </span>
          </li>
        ))}
      </ul>
      {check.notes && <p className="rounded-input bg-surface-2 px-2 py-1">Notes: {check.notes}</p>}
    </div>
  );
}
