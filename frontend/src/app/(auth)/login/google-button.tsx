"use client";

import Script from "next/script";
import { useCallback, useEffect, useRef, useState } from "react";

type GoogleId = {
  initialize(config: { client_id: string; callback: (response: { credential?: string }) => void; ux_mode?: "popup"; auto_select?: boolean }): void;
  renderButton(element: HTMLElement, options: Record<string, string | number>): void;
};

declare global {
  interface Window {
    google?: { accounts?: { id?: GoogleId } };
  }
}

/** Google Identity Services button. The credential goes to the BFF, never to the API from here. */
export function GoogleButton({ clientId, onCredential, disabled }: { clientId: string; onCredential: (credential: string) => void; disabled?: boolean }) {
  const container = useRef<HTMLDivElement>(null);
  const callback = useRef(onCredential);
  const [loaded, setLoaded] = useState(() => typeof window !== "undefined" && !!window.google?.accounts?.id);

  useEffect(() => {
    callback.current = onCredential;
  }, [onCredential]);

  const render = useCallback(() => {
    const gsi = window.google?.accounts?.id;
    if (!gsi || !container.current) return;
    gsi.initialize({
      client_id: clientId,
      ux_mode: "popup",
      callback: (response) => {
        if (response.credential) callback.current(response.credential);
      },
    });
    const dark = window.matchMedia("(prefers-color-scheme: dark)").matches || document.documentElement.dataset.theme === "dark";
    gsi.renderButton(container.current, {
      type: "standard",
      theme: dark ? "filled_black" : "outline",
      size: "large",
      text: "continue_with",
      shape: "rectangular",
      logo_alignment: "center",
      width: Math.min(container.current.clientWidth || 320, 400),
    });
  }, [clientId]);

  useEffect(() => {
    if (loaded) render();
  }, [loaded, render]);

  return (
    <>
      <Script src="https://accounts.google.com/gsi/client" strategy="afterInteractive" onLoad={() => setLoaded(true)} />
      <div ref={container} className={disabled ? "pointer-events-none flex min-h-11 justify-center opacity-60" : "flex min-h-11 justify-center"} aria-busy={!loaded} />
    </>
  );
}
