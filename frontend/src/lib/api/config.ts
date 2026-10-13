/** Server-side only. The browser never talks to the API. */
export function apiBaseUrl(): string {
  return (process.env.API_BASE_URL ?? "http://localhost:5178").replace(/\/+$/, "");
}

/** Read at request time (not inlined at build) so the compose `web` service can set it at runtime. */
export function googleClientId(): string {
  const name = "NEXT_PUBLIC_GOOGLE_CLIENT_ID";
  return (process.env[name] ?? "").trim();
}

/** Demo-account quick fill on /login: on in development, or when SHOW_DEMO_ACCOUNTS=true (demo stacks). */
export function showDemoAccounts(): boolean {
  const name = "SHOW_DEMO_ACCOUNTS";
  const flag = process.env[name];
  if (flag !== undefined) return flag === "true";
  return process.env.NODE_ENV !== "production";
}
