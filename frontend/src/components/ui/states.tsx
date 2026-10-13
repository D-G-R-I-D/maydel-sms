import type { ReactNode } from "react";
import { AlertTriangle, CircleCheck, Info, Lock, type LucideIcon, SearchX, XCircle } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { cn } from "./primitives";

type AlertTone = "info" | "success" | "warning" | "danger";
const alertStyles: Record<AlertTone, { icon: LucideIcon; classes: string }> = {
  info: { icon: Info, classes: "border-brand/30 bg-brand/5 text-text [&_svg]:text-brand" },
  success: { icon: CircleCheck, classes: "border-success/30 bg-success/8 text-text [&_svg]:text-success" },
  warning: { icon: AlertTriangle, classes: "border-warning/40 bg-warning/8 text-text [&_svg]:text-warning" },
  danger: { icon: XCircle, classes: "border-danger/30 bg-danger/8 text-text [&_svg]:text-danger" },
};

/** Status is never colour alone: every alert carries an icon and text. */
export function Alert({ tone = "info", title, children, className }: { tone?: AlertTone; title?: string; children?: ReactNode; className?: string }) {
  const { icon: Icon, classes } = alertStyles[tone];
  return (
    <div role={tone === "danger" || tone === "warning" ? "alert" : "status"} className={cn("flex gap-3 rounded-input border p-3 text-sm", classes, className)}>
      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden />
      <div className="space-y-1">
        {title && <p className="font-medium">{title}</p>}
        {children && <div>{children}</div>}
      </div>
    </div>
  );
}

export function TraceId({ traceId }: { traceId?: string }) {
  if (!traceId) return null;
  return (
    <p className="text-xs text-muted">
      Quote this to support: <code className="select-all font-mono">{traceId}</code>
    </p>
  );
}

export function EmptyState({ icon: Icon = SearchX, title, children, action }: { icon?: LucideIcon; title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center px-6 py-12 text-center">
      <span className="mb-3 inline-flex size-12 items-center justify-center rounded-full bg-surface-2 text-muted">
        <Icon className="size-6" aria-hidden />
      </span>
      <h2 className="font-display text-lg font-semibold">{title}</h2>
      {children && <div className="mt-1 max-w-md text-sm text-muted">{children}</div>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

/** Error state for a data view that failed to load. */
export function ProblemState({ problem, action }: { problem: Problem; action?: ReactNode }) {
  if (problem.status === 403) return <AccessDenied traceId={problem.traceId} />;
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      <span className="inline-flex size-12 items-center justify-center rounded-full bg-danger/10 text-danger">
        <AlertTriangle className="size-6" aria-hidden />
      </span>
      <h2 className="font-display text-lg font-semibold">We couldn&apos;t load this</h2>
      <p className="max-w-md text-sm text-muted">{problemMessage(problem)}</p>
      <TraceId traceId={problem.traceId} />
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function AccessDenied({ traceId }: { traceId?: string }) {
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      <span className="inline-flex size-12 items-center justify-center rounded-full bg-surface-2 text-muted">
        <Lock className="size-6" aria-hidden />
      </span>
      <h2 className="font-display text-lg font-semibold">You don&apos;t have access to this page</h2>
      <p className="max-w-md text-sm text-muted">If you think you should, ask the school administrator to check your role.</p>
      <TraceId traceId={traceId} />
    </div>
  );
}
