/** RFC 9457 problem details as the API returns them (ADR-005 §4). */
export type Problem = {
  status: number;
  title: string;
  detail?: string;
  code?: string;
  traceId?: string;
  /** Only on 400 `validation`, keyed by camelCase field name. */
  errors?: Record<string, string[]>;
};

export const ProblemCodes = {
  validation: "validation",
  invalidCredentials: "auth.invalid_credentials",
  sessionExpired: "auth.session_expired",
  googleDisabled: "auth.google_disabled",
  passwordChangeRequired: "auth.password_change_required",
} as const;

const fallbackTitles: Record<number, string> = {
  400: "Please check the highlighted fields.",
  401: "Your session has ended. Please sign in again.",
  403: "You don't have access to this page.",
  404: "We couldn't find what you were looking for.",
  429: "Too many attempts. Please wait a minute and try again.",
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

/** Normalises whatever came back (problem JSON, empty body, HTML from a proxy) into a Problem. */
export function toProblem(status: number, body: unknown): Problem {
  const b = isRecord(body) ? body : {};
  const errors = isRecord(b.errors)
    ? Object.fromEntries(
        Object.entries(b.errors).map(([k, v]) => [k, Array.isArray(v) ? v.filter((x): x is string => typeof x === "string") : []]),
      )
    : undefined;
  const statusFromBody = typeof b.status === "number" ? b.status : undefined;
  const finalStatus = statusFromBody ?? status;
  return {
    status: finalStatus,
    title: asString(b.title) ?? fallbackTitles[finalStatus] ?? "Something went wrong.",
    detail: asString(b.detail),
    code: asString(b.code),
    traceId: asString(b.traceId),
    errors,
  };
}

/** A problem for when the API could not be reached at all. */
export function unreachableProblem(): Problem {
  return { status: 503, title: "The school system is unavailable right now. Please try again shortly." };
}

/** The sentence to show a user: `detail` is written for them; `title` is the fallback. */
export function problemMessage(problem: Problem): string {
  if (problem.status === 429) return problem.detail ?? fallbackTitles[429];
  return problem.detail ?? problem.title;
}

export function isProblem(value: unknown): value is Problem {
  return isRecord(value) && typeof value.status === "number" && typeof value.title === "string";
}
