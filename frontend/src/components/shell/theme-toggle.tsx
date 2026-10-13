"use client";

import { Monitor, Moon, Sun } from "lucide-react";
import * as Menu from "@radix-ui/react-dropdown-menu";
import { useState } from "react";
import { THEME_COOKIE, parseTheme, type Theme } from "@/lib/theme";

function readTheme(): Theme {
  if (typeof document === "undefined") return "system";
  const match = document.cookie.match(new RegExp(`(?:^|; )${THEME_COOKIE}=([^;]*)`));
  return parseTheme(match?.[1]);
}

function applyTheme(theme: Theme) {
  const root = document.documentElement;
  if (theme === "system") delete root.dataset.theme;
  else root.dataset.theme = theme;
  document.cookie = `${THEME_COOKIE}=${theme}; Path=/; Max-Age=31536000; SameSite=Lax`;
}

const options: { value: Theme; label: string; icon: typeof Sun }[] = [
  { value: "system", label: "System", icon: Monitor },
  { value: "light", label: "Light", icon: Sun },
  { value: "dark", label: "Dark", icon: Moon },
];

/** Theme radio group for the user menu. The choice is a plain (non-sensitive) cookie so SSR renders it. */
export function ThemeMenuItems() {
  const [theme, setTheme] = useState<Theme>(readTheme);
  return (
    <>
      <Menu.Label className="px-3 pt-2 pb-1 text-xs font-medium text-muted">Theme</Menu.Label>
      <Menu.RadioGroup
        value={theme}
        onValueChange={(v) => {
          const next = parseTheme(v);
          setTheme(next);
          applyTheme(next);
        }}
      >
        {options.map(({ value, label, icon: Icon }) => (
          <Menu.RadioItem
            key={value}
            value={value}
            onSelect={(e) => e.preventDefault()}
            className="flex min-h-10 cursor-pointer items-center gap-2 rounded-input px-3 text-sm outline-none data-[highlighted]:bg-surface-2 data-[state=checked]:font-medium data-[state=checked]:text-brand"
          >
            <Icon className="size-4" aria-hidden />
            {label}
          </Menu.RadioItem>
        ))}
      </Menu.RadioGroup>
    </>
  );
}
