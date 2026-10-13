import { problemMessage, type Problem } from "@/lib/api/problem";
import type { Loaded } from "@/lib/api/server";
import { toast } from "@/components/ui/toast";
import { applyFieldErrors } from "./bff";

/**
 * Standard handling for a Server Action result in a form:
 * - success → success toast, returns the data;
 * - 400 with field errors → shown under the fields;
 * - anything else → returned so the form can show it inline (409 / 422 `detail` is written for users),
 *   with transient failures (network, 429, 5xx) also raised as a toast.
 */
export async function runAction<T, F extends string>(
  action: () => Promise<Loaded<T>>,
  opts: {
    success: string | ((data: T) => string);
    fields?: readonly F[];
    setError?: (field: F, error: { type: string; message: string }) => void;
    onProblem?: (problem: Problem) => void;
  },
): Promise<T | null> {
  let result: Loaded<T>;
  try {
    result = await action();
  } catch (error) {
    // redirect() from the action (session refresh / password change) must propagate.
    if (error && typeof error === "object" && "digest" in error && String((error as { digest: unknown }).digest).startsWith("NEXT_REDIRECT")) throw error;
    const problem: Problem = { status: 0, title: "You appear to be offline. Check your connection and try again." };
    toast.error(problemMessage(problem));
    opts.onProblem?.(problem);
    return null;
  }
  if (result.ok) {
    toast.success(typeof opts.success === "function" ? opts.success(result.data) : opts.success);
    return result.data;
  }
  const p = result.problem;
  if (opts.fields && opts.setError && applyFieldErrors(p, opts.fields, opts.setError)) return null;
  if (p.status === 0 || p.status === 429 || p.status >= 500) {
    toast.error(problemMessage(p), { description: p.traceId ? `Reference: ${p.traceId}` : undefined });
  }
  opts.onProblem?.(p);
  return null;
}
