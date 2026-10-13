import "server-only";
import { load } from "@/lib/api/server";
import type { TermOption } from "@/components/assessment/term-picker";

/**
 * Terms for the picker (newest session first) and the one to show: `?termId=` if valid,
 * else the current term, else the newest term.
 */
export async function loadTerms(requested: string | undefined): Promise<{ terms: TermOption[]; selected: TermOption | null }> {
  const sessions = await load((api) => api.GET("/api/academics/sessions"));
  const terms: TermOption[] = sessions.ok
    ? sessions.data.flatMap((s) => s.terms.map((t) => ({ id: t.id, label: `${t.number} Term`, session: s.name, isCurrent: t.isCurrent })))
    : [];
  const selected = terms.find((t) => t.id === requested) ?? terms.find((t) => t.isCurrent) ?? terms[0] ?? null;
  return { terms, selected };
}
