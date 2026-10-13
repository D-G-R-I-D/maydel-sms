import { isProblem, toProblem, type Problem } from "@/lib/api/problem";

export type BffResult<T> = { ok: true; data: T } | { ok: false; problem: Problem };

/** Browser → BFF route handler. Same-origin only; never carries a token. */
export async function postJson<T>(url: string, body: unknown): Promise<BffResult<T>> {
  let res: Response;
  try {
    res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(body),
      credentials: "same-origin",
    });
  } catch {
    return { ok: false, problem: { status: 0, title: "You appear to be offline. Check your connection and try again." } };
  }
  const json: unknown = res.status === 204 ? null : await res.json().catch(() => null);
  if (res.ok) return { ok: true, data: json as T };
  return { ok: false, problem: isProblem(json) ? json : toProblem(res.status, json) };
}

/** Applies `errors[field]` from a 400 problem to form fields; returns true if any were applied. */
export function applyFieldErrors<F extends string>(problem: Problem, fields: readonly F[], setError: (field: F, error: { type: string; message: string }) => void): boolean {
  if (problem.status !== 400 || !problem.errors) return false;
  let applied = false;
  for (const [key, messages] of Object.entries(problem.errors)) {
    const field = fields.find((f) => f.toLowerCase() === key.toLowerCase());
    if (field && messages.length > 0) {
      setError(field, { type: "server", message: messages.join(" ") });
      applied = true;
    }
  }
  return applied;
}
