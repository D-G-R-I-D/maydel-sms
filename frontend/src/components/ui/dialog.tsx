"use client";

import * as Dialog from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Button, cn } from "./primitives";

/**
 * Modal shell for add/edit forms: focus trap, Esc to close, labelled by its title. Full-height
 * sheet on phones, centred card from sm up. `children` gets a `close()` for after a save.
 */
export function FormDialog({
  trigger,
  title,
  description,
  open: controlledOpen,
  onOpenChange,
  size = "md",
  children,
}: {
  trigger?: ReactNode;
  size?: "md" | "lg";
  title: string;
  description?: ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  children: (close: () => void) => ReactNode;
}) {
  const [uncontrolled, setUncontrolled] = useState(false);
  const open = controlledOpen ?? uncontrolled;
  const setOpen = (v: boolean) => {
    if (controlledOpen === undefined) setUncontrolled(v);
    onOpenChange?.(v);
  };

  return (
    <Dialog.Root open={open} onOpenChange={setOpen}>
      {trigger && <Dialog.Trigger asChild>{trigger}</Dialog.Trigger>}
      <Dialog.Portal>
        <Dialog.Overlay className="animate-fade fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px]" />
        <Dialog.Content
          className={cn(
            "animate-rise fixed z-50 flex flex-col overflow-hidden border border-border bg-surface text-text shadow-2xl",
            "inset-x-0 bottom-0 max-h-[92dvh] rounded-t-panel",
            size === "lg" ? "sm:max-w-3xl" : "sm:max-w-lg",
            "sm:inset-auto sm:top-1/2 sm:left-1/2 sm:max-h-[85dvh] sm:w-[calc(100%-2rem)] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-card",
          )}
        >
          <div className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
            <div>
              <Dialog.Title className="font-display text-xl font-semibold">{title}</Dialog.Title>
              {description ? (
                <Dialog.Description className="mt-0.5 text-sm text-muted">{description}</Dialog.Description>
              ) : (
                <Dialog.Description className="sr-only">{title}</Dialog.Description>
              )}
            </div>
            <Dialog.Close className="-mr-2 inline-flex size-10 shrink-0 items-center justify-center rounded-input text-muted hover:bg-surface-2 hover:text-text" aria-label="Close">
              <X className="size-5" aria-hidden />
            </Dialog.Close>
          </div>
          <div className="overflow-y-auto px-5 py-4">{children(() => setOpen(false))}</div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/** Footer row for forms inside FormDialog. */
export function DialogActions({ children }: { children: ReactNode }) {
  return <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">{children}</div>;
}

export function CancelButton() {
  return (
    <Dialog.Close asChild>
      <Button variant="secondary">Cancel</Button>
    </Dialog.Close>
  );
}

/**
 * Confirmation for consequential actions. role="alertdialog"; the safe choice (Cancel) gets focus.
 */
export function ConfirmDialog({
  trigger,
  title,
  children,
  confirmLabel,
  tone = "primary",
  onConfirm,
  open: controlledOpen,
  onOpenChange,
}: {
  trigger?: ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  title: string;
  children: ReactNode;
  confirmLabel: string;
  tone?: "primary" | "destructive";
  onConfirm: () => Promise<boolean>;
}) {
  const [uncontrolled, setUncontrolled] = useState(false);
  const [busy, setBusy] = useState(false);
  const open = controlledOpen ?? uncontrolled;
  const setOpen = (v: boolean) => {
    if (controlledOpen === undefined) setUncontrolled(v);
    onOpenChange?.(v);
  };
  return (
    <Dialog.Root open={open} onOpenChange={(v) => !busy && setOpen(v)}>
      {trigger && <Dialog.Trigger asChild>{trigger}</Dialog.Trigger>}
      <Dialog.Portal>
        <Dialog.Overlay className="animate-fade fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px]" />
        <Dialog.Content
          role="alertdialog"
          className="animate-rise fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-card border border-border bg-surface p-5 text-text shadow-2xl"
          onOpenAutoFocus={(e) => {
            e.preventDefault();
            (e.currentTarget as HTMLElement).querySelector<HTMLButtonElement>("[data-cancel]")?.focus();
          }}
        >
          <Dialog.Title className="font-display text-lg font-semibold">{title}</Dialog.Title>
          <Dialog.Description asChild>
            <div className="mt-2 text-sm text-muted">{children}</div>
          </Dialog.Description>
          <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <Dialog.Close asChild>
              <Button variant="secondary" data-cancel disabled={busy}>
                Cancel
              </Button>
            </Dialog.Close>
            <Button
              variant={tone}
              loading={busy}
              onClick={async () => {
                setBusy(true);
                const ok = await onConfirm().finally(() => setBusy(false));
                if (ok) setOpen(false);
              }}
            >
              {confirmLabel}
            </Button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
