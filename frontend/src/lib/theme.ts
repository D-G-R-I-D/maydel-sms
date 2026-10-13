export const THEME_COOKIE = "theme";
export const Themes = ["system", "light", "dark"] as const;
export type Theme = (typeof Themes)[number];

export function parseTheme(value: string | undefined): Theme {
  return (Themes as readonly string[]).includes(value ?? "") ? (value as Theme) : "system";
}
