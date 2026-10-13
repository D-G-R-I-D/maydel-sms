"use client";

import { useEffect } from "react";
import { Toaster as Sonner, toast } from "sonner";
import { CircleCheck, Info, Loader2, TriangleAlert, XCircle } from "lucide-react";
import { FLASH_COOKIE, flashMessages, parseFlash } from "@/lib/flash";

export { toast };

/** Shows a one-shot message left by a BFF route before a full-page redirect, then forgets it. */
function FlashReader() {
  useEffect(() => {
    const match = document.cookie.match(new RegExp(`(?:^|; )${FLASH_COOKIE}=([^;]*)`));
    if (!match) return;
    document.cookie = `${FLASH_COOKIE}=; Path=/; Max-Age=0; SameSite=Lax`;
    const flash = parseFlash(decodeURIComponent(match[1]));
    if (!flash) return;
    const { tone, title, description } = flashMessages[flash.kind](flash.name);
    toast[tone](title, { description });
  }, []);
  return null;
}

/** Bottom-centre on phones (thumb reach), bottom-right on larger screens; themed with our tokens. */
export function Toaster() {
  return (
    <>
      <Sonner
        position="bottom-right"
        closeButton
        duration={5000}
        visibleToasts={3}
        offset={16}
        mobileOffset={{ bottom: 88 }}
        icons={{
          success: <CircleCheck className="size-4 text-success" aria-hidden />,
          info: <Info className="size-4 text-brand" aria-hidden />,
          warning: <TriangleAlert className="size-4 text-warning" aria-hidden />,
          error: <XCircle className="size-4 text-danger" aria-hidden />,
          loading: <Loader2 className="size-4 animate-spin text-muted" aria-hidden />,
        }}
        toastOptions={{
          unstyled: true,
          classNames: {
            toast:
              "group pointer-events-auto flex w-full items-start gap-3 rounded-card border border-border bg-surface p-4 pr-8 text-sm text-text shadow-lg sm:w-[22rem]",
            title: "font-medium",
            description: "mt-0.5 text-muted",
            icon: "mt-0.5",
            closeButton:
              "absolute top-2 right-2 inline-flex size-6 items-center justify-center rounded-full text-muted hover:bg-surface-2 hover:text-text [&_svg]:size-3.5",
            actionButton: "rounded-input bg-brand px-2.5 py-1 text-xs font-medium text-on-brand",
          },
        }}
      />
      <FlashReader />
    </>
  );
}
