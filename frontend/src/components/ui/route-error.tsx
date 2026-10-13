"use client";

import { AlertTriangle, RotateCcw } from "lucide-react";
import { Button } from "./primitives";

/** Shared body for error.tsx boundaries: unexpected failures only (expected ones are Problem states). */
export function RouteError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-2 px-6 py-16 text-center">
      <span className="inline-flex size-12 items-center justify-center rounded-full bg-danger/10 text-danger">
        <AlertTriangle className="size-6" aria-hidden />
      </span>
      <h1 className="font-display text-xl font-semibold">Something went wrong</h1>
      <p className="max-w-md text-sm text-muted">This page hit an unexpected error. Trying again usually fixes it.</p>
      {error.digest && (
        <p className="text-xs text-muted">
          Quote this to support: <code className="select-all font-mono">{error.digest}</code>
        </p>
      )}
      <Button variant="secondary" className="mt-2" onClick={reset}>
        <RotateCcw className="size-4" aria-hidden />
        Try again
      </Button>
    </div>
  );
}
