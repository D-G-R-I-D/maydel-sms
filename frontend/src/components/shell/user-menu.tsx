"use client";

import * as Menu from "@radix-ui/react-dropdown-menu";
import Link from "next/link";
import { ChevronDown, KeyRound, LogOut } from "lucide-react";
import { useRef } from "react";
import { Avatar } from "@/components/ui/primitives";
import { ThemeMenuItems } from "./theme-toggle";

const itemClasses =
  "flex min-h-10 w-full cursor-pointer items-center gap-2 rounded-input px-3 text-sm text-text outline-none data-[highlighted]:bg-surface-2";

export function UserMenu({ fullName, userName, roles }: { fullName: string; userName: string; roles: readonly string[] }) {
  const signOutForm = useRef<HTMLFormElement>(null);
  return (
    <>
      <form ref={signOutForm} action="/api/auth/logout" method="post" className="hidden" aria-hidden data-sign-out />
      <Menu.Root>
        <Menu.Trigger
          className="flex min-h-11 items-center gap-2 rounded-input px-2 text-left hover:bg-surface-2 data-[state=open]:bg-surface-2"
          aria-label={`Account menu for ${fullName}`}
        >
          <Avatar name={fullName} />
          <span className="hidden min-w-0 sm:block">
            <span className="block truncate text-sm font-medium">{fullName}</span>
            <span className="block truncate text-xs text-muted">{roles.join(", ")}</span>
          </span>
          <ChevronDown className="size-4 text-muted" aria-hidden />
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Content
            align="end"
            sideOffset={6}
            className="z-50 w-64 rounded-card border border-border bg-surface p-1.5 text-text shadow-low"
          >
            <div className="px-3 py-2">
              <p className="truncate text-sm font-medium">{fullName}</p>
              <p className="truncate text-xs text-muted">{userName}</p>
            </div>
            <Menu.Separator className="my-1 h-px bg-border" />
            <Menu.Item asChild className={itemClasses}>
              <Link href="/change-password">
                <KeyRound className="size-4" aria-hidden />
                Change password
              </Link>
            </Menu.Item>
            <Menu.Separator className="my-1 h-px bg-border" />
            <ThemeMenuItems />
            <Menu.Separator className="my-1 h-px bg-border" />
            <Menu.Item className={itemClasses} onSelect={() => signOutForm.current?.requestSubmit()}>
              <LogOut className="size-4" aria-hidden />
              Sign out
            </Menu.Item>
          </Menu.Content>
        </Menu.Portal>
      </Menu.Root>
    </>
  );
}
