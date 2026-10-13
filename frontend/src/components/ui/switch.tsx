"use client";

import { cn } from "./primitives";

/** Accessible on/off switch (role="switch"). Pair with a visible label via `aria-labelledby` or `label`. */
export function Switch({
  checked,
  onCheckedChange,
  label,
  disabled,
  className,
  id,
}: {
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  label: string;
  disabled?: boolean;
  className?: string;
  id?: string;
}) {
  return (
    <button
      id={id}
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => onCheckedChange(!checked)}
      className={cn(
        "relative inline-flex h-6 w-11 shrink-0 items-center rounded-full border border-transparent transition-colors duration-200 ease-out",
        "disabled:cursor-not-allowed disabled:opacity-50",
        checked ? "bg-success" : "bg-border",
        className,
      )}
    >
      <span
        aria-hidden
        className={cn(
          "inline-block size-5 rounded-full bg-white shadow transition-transform duration-200 ease-out",
          checked ? "translate-x-5" : "translate-x-0.5",
        )}
      />
    </button>
  );
}
