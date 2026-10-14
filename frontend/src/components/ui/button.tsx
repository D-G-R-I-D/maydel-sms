"use client";

import { forwardRef, useLayoutEffect, useRef, type ButtonHTMLAttributes, type ReactNode } from "react";
import { SchoolLoader } from "./school-loader";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "destructive" | "link";
export type ButtonSize = "sm" | "md" | "lg";

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Pending: shows the small crest loader + `loadingText` and disables the button. */
  loading?: boolean;
  /** Verb shown while pending, e.g. "Saving…". Defaults to the button's own label. */
  loadingText?: ReactNode;
};

/**
 * Renders exactly one label at a time (idle children, or crest + verb while pending). When it goes
 * pending it pins its current width, so "Save" → "Saving…" never makes the layout jump; the pin is
 * released when it settles. No reliance on stacked/hidden duplicate labels.
 */
export const PendingButton = forwardRef<HTMLButtonElement, ButtonProps & { classes: string }>(function PendingButton(
  { loading = false, loadingText, classes, children, disabled, type = "button", ...rest },
  forwarded,
) {
  const local = useRef<HTMLButtonElement | null>(null);

  useLayoutEffect(() => {
    const el = local.current;
    if (!el) return;
    if (loading) el.style.minWidth = `${el.getBoundingClientRect().width}px`;
    else el.style.removeProperty("min-width");
  }, [loading]);

  return (
    <button
      ref={(el) => {
        local.current = el;
        if (typeof forwarded === "function") forwarded(el);
        else if (forwarded) forwarded.current = el;
      }}
      type={type}
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? (
        <>
          <SchoolLoader size="xs" />
          <span className="truncate">{loadingText ?? children}</span>
        </>
      ) : (
        children
      )}
    </button>
  );
});
