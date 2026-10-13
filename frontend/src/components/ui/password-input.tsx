"use client";

import { forwardRef, useState, type InputHTMLAttributes, type KeyboardEvent } from "react";
import { ArrowBigUpDash, Eye, EyeOff } from "lucide-react";
import { Input, cn } from "./primitives";

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, "type">;

/** Password field with a show/hide toggle and a Caps Lock warning. */
export const PasswordInput = forwardRef<HTMLInputElement, Props>(function PasswordInput({ className, onKeyUp, onKeyDown, onBlur, ...rest }, ref) {
  const [visible, setVisible] = useState(false);
  const [capsLock, setCapsLock] = useState(false);
  const track = (e: KeyboardEvent<HTMLInputElement>) => setCapsLock(e.getModifierState?.("CapsLock") ?? false);

  return (
    <div>
      <div className="relative">
        <Input
          ref={ref}
          type={visible ? "text" : "password"}
          className={cn("pr-12", className)}
          onKeyDown={(e) => {
            track(e);
            onKeyDown?.(e);
          }}
          onKeyUp={(e) => {
            track(e);
            onKeyUp?.(e);
          }}
          onBlur={(e) => {
            setCapsLock(false);
            onBlur?.(e);
          }}
          {...rest}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          className="absolute inset-y-0 right-0 flex w-11 items-center justify-center rounded-r-input text-muted transition-colors hover:text-text"
          aria-label={visible ? "Hide password" : "Show password"}
          aria-pressed={visible}
        >
          {visible ? <EyeOff className="size-4" aria-hidden /> : <Eye className="size-4" aria-hidden />}
        </button>
      </div>
      {capsLock && (
        <p className="animate-fade mt-1.5 flex items-center gap-1.5 text-xs font-medium text-warning" role="status">
          <ArrowBigUpDash className="size-3.5" aria-hidden />
          Caps Lock is on
        </p>
      )}
    </div>
  );
});
