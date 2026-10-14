import type { Problem } from "@/lib/api/problem";

/**
 * The problem for a request that threw before any response (fetch / Server Action failure).
 * Only claim "offline" when the browser says so; otherwise something between us and the server
 * refused it (a stale page after a deploy, a proxy), and a reload usually fixes it.
 */
export function unreachableProblem(online: boolean = typeof navigator === "undefined" || navigator.onLine): Problem {
  return online
    ? { status: 0, title: "Couldn't reach the server. Reload the page and try again." }
    : { status: 0, title: "You appear to be offline. Check your connection and try again." };
}
