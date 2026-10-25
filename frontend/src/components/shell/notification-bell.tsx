"use client";

import * as Menu from "@radix-ui/react-dropdown-menu";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, Bell as BellIcon, BookOpenCheck, Bus, CheckCheck, ClipboardList, Megaphone, NotebookPen, Receipt, type LucideIcon } from "lucide-react";
import { loadBell, markAllRead, markNoticeRead, markNotificationRead } from "@/lib/notifications/actions";
import { ago, badgeText, safeLink, type Bell, type NotificationKind } from "@/lib/notifications/bell";
import { cn } from "@/components/ui/primitives";

// Partial: a kind added on the server shows with the plain bell until it gets its own icon here.
const kindIcon: Partial<Record<NotificationKind, { icon: LucideIcon; tone: string }>> = {
  PaymentReceived: { icon: Receipt, tone: "bg-success/15 text-success" },
  FeesOverdue: { icon: AlertTriangle, tone: "bg-warning/15 text-warning" },
  HomeworkSet: { icon: ClipboardList, tone: "bg-brand/10 text-brand" },
  HomeworkReviewed: { icon: BookOpenCheck, tone: "bg-success/15 text-success" },
  BookEntry: { icon: NotebookPen, tone: "bg-brand/10 text-brand" },
  BusCheckFlagged: { icon: Bus, tone: "bg-warning/15 text-warning" },
};

const itemClasses = "flex w-full cursor-pointer gap-3 rounded-input px-3 py-2.5 text-left outline-none data-[highlighted]:bg-surface-2";

/**
 * The header bell (E12.8): payment and overdue notifications first, then school notices. The badge
 * counts both. Opening an item marks it read and follows its link. Refreshes every minute while the
 * tab is visible, and whenever the menu opens (layouts don't re-render on client navigation).
 */
export function NotificationBell({ initial, noticesHref }: { initial: Bell; noticesHref: string }) {
  const router = useRouter();
  const [bell, setBell] = useState(initial);

  const refresh = useCallback(async () => {
    try {
      setBell(await loadBell());
    } catch {
      // Keep what's showing; the next refresh tries again.
    }
  }, []);

  useEffect(() => {
    const id = window.setInterval(() => {
      if (document.visibilityState === "visible") void refresh();
    }, 60_000);
    return () => window.clearInterval(id);
  }, [refresh]);

  const open = async (kind: "notification" | "notice", id: string, href: string | null, wasRead: boolean) => {
    if (!wasRead) {
      // Optimistic: the badge drops at once; a failed call is corrected by the next refresh.
      setBell((b) => ({
        ...b,
        unread: Math.max(0, b.unread - 1),
        notifications: kind === "notification" ? b.notifications.map((n) => (n.id === id ? { ...n, isRead: true } : n)) : b.notifications,
        notices: kind === "notice" ? b.notices.map((n) => (n.id === id ? { ...n, isRead: true } : n)) : b.notices,
      }));
      void (kind === "notification" ? markNotificationRead(id) : markNoticeRead(id)).catch(() => undefined);
    }
    if (href) router.push(href);
  };

  const readAll = async () => {
    setBell((b) => ({ ...b, unread: 0, notifications: b.notifications.map((n) => ({ ...n, isRead: true })), notices: b.notices.map((n) => ({ ...n, isRead: true })) }));
    await markAllRead().catch(() => undefined);
  };

  const badge = badgeText(bell.unread);
  const notices = bell.notices.slice(0, 5);
  const empty = bell.notifications.length === 0 && notices.length === 0;

  return (
    <Menu.Root onOpenChange={(o) => o && void refresh()}>
      <Menu.Trigger
        className="relative inline-flex size-11 items-center justify-center rounded-input text-muted hover:bg-surface-2 hover:text-text data-[state=open]:bg-surface-2"
        aria-label={bell.unread > 0 ? `Notifications, ${bell.unread} unread` : "Notifications"}
      >
        <BellIcon className="size-5" aria-hidden />
        {badge && (
          <span className="tabular absolute top-1.5 right-1 inline-flex min-w-5 items-center justify-center rounded-full bg-crimson px-1 text-[0.6875rem] leading-5 font-semibold text-white" aria-hidden>
            {badge}
          </span>
        )}
      </Menu.Trigger>
      <Menu.Portal>
        <Menu.Content align="end" sideOffset={6} className="animate-rise z-50 w-[min(24rem,calc(100vw-1rem))] overflow-hidden rounded-card border border-border bg-surface text-text shadow-2xl">
          <div className="flex items-center justify-between gap-3 border-b border-border px-4 py-3">
            <p className="font-display text-base font-semibold">Notifications</p>
            {bell.unread > 0 && (
              <Menu.Item onSelect={(e) => (e.preventDefault(), void readAll())} className="inline-flex cursor-pointer items-center gap-1 rounded-input px-2 py-1 text-xs font-medium text-brand outline-none data-[highlighted]:bg-surface-2">
                <CheckCheck className="size-3.5" aria-hidden />
                Mark all read
              </Menu.Item>
            )}
          </div>
          <div className="max-h-[min(28rem,70dvh)] overflow-y-auto p-1.5">
            {empty ? (
              <p className="px-3 py-8 text-center text-sm text-muted">{bell.failed ? "Couldn't load notifications. Try again shortly." : "You're all caught up."}</p>
            ) : (
              <>
                {bell.notifications.map((n) => {
                  const { icon: Icon, tone } = kindIcon[n.kind] ?? { icon: BellIcon, tone: "bg-brand/10 text-brand" };
                  return (
                    <Menu.Item key={n.id} className={itemClasses} onSelect={() => void open("notification", n.id, safeLink(n.linkUrl), n.isRead)}>
                      <span className={cn("inline-flex size-9 shrink-0 items-center justify-center rounded-full", tone)}>
                        <Icon className="size-4" aria-hidden />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className={cn("block text-sm", !n.isRead && "font-semibold")}>{n.title}</span>
                        <span className="line-clamp-2 block text-xs text-muted">{n.body}</span>
                        <span className="mt-0.5 block text-[0.6875rem] text-muted">{ago(n.createdAt)}</span>
                      </span>
                      {!n.isRead && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-brand" aria-label="Unread" />}
                    </Menu.Item>
                  );
                })}
                {notices.length > 0 && (
                  <>
                    <Menu.Label className="px-3 pt-3 pb-1 text-xs font-semibold tracking-wider text-muted uppercase">School notices</Menu.Label>
                    {notices.map((n) => (
                      <Menu.Item key={n.id} className={itemClasses} onSelect={() => void open("notice", n.id, `${noticesHref}#notice-${n.id}`, n.isRead)}>
                        <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-full bg-brand/10 text-brand">
                          <Megaphone className="size-4" aria-hidden />
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className={cn("block text-sm", !n.isRead && "font-semibold")}>{n.title}</span>
                          {n.summary && <span className="line-clamp-2 block text-xs text-muted">{n.summary}</span>}
                          <span className="mt-0.5 block text-[0.6875rem] text-muted">{ago(n.publishedAt)}</span>
                        </span>
                        {!n.isRead && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-brand" aria-label="Unread" />}
                      </Menu.Item>
                    ))}
                    <Menu.Item className="block cursor-pointer rounded-input px-3 py-2 text-center text-sm font-medium text-brand outline-none data-[highlighted]:bg-surface-2" onSelect={() => router.push(noticesHref)}>
                      All school notices
                    </Menu.Item>
                  </>
                )}
              </>
            )}
          </div>
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}
