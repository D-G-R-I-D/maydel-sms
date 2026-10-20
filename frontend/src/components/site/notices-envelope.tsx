"use client";

import Link from "next/link";
import { useSyncExternalStore } from "react";
import { ArrowRight, Mail } from "lucide-react";
import type { components } from "@/lib/api/schema";
import { Badge, cn } from "@/components/ui/primitives";
import { FormDialog } from "@/components/ui/dialog";

type Notice = components["schemas"]["PublicNotice"];

// Per-browser memory of what's been read and which pop-ups have shown, keyed id:version so an edit
// to a notice shows it again. Storage can be blocked (private mode): then everything just looks new.
const READ = "maydel.notices.read";
const POPPED = "maydel.notices.popped";
const EVENT = "maydel-notices";
const keyOf = (n: Notice) => `${n.id}:${n.version}`;

function readList(key: string): string[] {
  try {
    const v = JSON.parse(localStorage.getItem(key) ?? "[]");
    return Array.isArray(v) ? v.filter((x): x is string => typeof x === "string") : [];
  } catch {
    return [];
  }
}
function addTo(key: string, items: string[]) {
  try {
    localStorage.setItem(key, JSON.stringify([...new Set([...readList(key), ...items])].slice(-200)));
  } catch {
    // Blocked storage: nothing to remember.
  }
  window.dispatchEvent(new Event(EVENT));
}
const subscribe = (cb: () => void) => {
  window.addEventListener(EVENT, cb);
  window.addEventListener("storage", cb);
  return () => {
    window.removeEventListener(EVENT, cb);
    window.removeEventListener("storage", cb);
  };
};
const snapshot = () => {
  try {
    return `${localStorage.getItem(READ) ?? ""}|${localStorage.getItem(POPPED) ?? ""}`;
  } catch {
    return "|";
  }
};

function NoticeBody({ n }: { n: Notice }) {
  return (
    <article className="space-y-2">
      <h3 className="font-display text-base font-semibold">{n.title}</h3>
      <p className="text-sm whitespace-pre-line text-text">{n.body || n.summary}</p>
      {n.linkUrl && (
        <Link href={n.linkUrl} className="inline-flex items-center gap-1 text-sm font-medium text-brand hover:underline">
          {n.linkText || "Learn more"}
          <ArrowRight className="size-4" aria-hidden />
        </Link>
      )}
    </article>
  );
}

/**
 * E7.10: the notices envelope. A button with the unread count opens current notices; a featured
 * notice pops up once per visitor per version. It never blocks the page; Esc or the close button dismiss it.
 */
export function NoticesEnvelope({ notices, raised = false }: { notices: Notice[]; /** Sit above the WhatsApp button. */ raised?: boolean }) {
  // null on the server and on first paint, so hydration matches; then the browser's real memory.
  const stored = useSyncExternalStore(subscribe, snapshot, () => null);
  if (notices.length === 0) return null;
  const read = stored === null ? null : new Set(readList(READ));
  const popped = stored === null ? null : new Set(readList(POPPED));
  const unread = read ? notices.filter((n) => !read.has(keyOf(n))).length : 0;
  const popup = popped ? notices.find((n) => n.isFeatured && !popped.has(keyOf(n))) : undefined;

  return (
    <>
      <FormDialog
        title="Notices"
        description="Current announcements from the school."
        onOpenChange={(v) => v && addTo(READ, notices.map(keyOf))}
        trigger={
          <button
            type="button"
            className={cn(
              "fixed right-4 z-30 inline-flex min-h-12 items-center gap-2 rounded-full border border-border bg-surface px-4 text-sm font-medium shadow-lg hover:bg-surface-2",
              raised ? "bottom-22" : "bottom-4",
            )}
            aria-label={unread > 0 ? `Notices, ${unread} unread` : "Notices"}
          >
            <Mail className="size-5 text-brand" aria-hidden />
            Notices
            {unread > 0 && <Badge tone="danger">{unread}</Badge>}
          </button>
        }
      >
        {() => (
          <ul className="space-y-5 divide-y divide-border">
            {notices.map((n) => (
              <li key={n.id} className="pt-4 first:pt-0">
                <NoticeBody n={n} />
              </li>
            ))}
          </ul>
        )}
      </FormDialog>
      {popup && (
        <FormDialog
          title="Notice"
          open
          onOpenChange={(v) => {
            if (!v) addTo(POPPED, [keyOf(popup)]);
          }}
        >
          {() => <NoticeBody n={popup} />}
        </FormDialog>
      )}
    </>
  );
}
