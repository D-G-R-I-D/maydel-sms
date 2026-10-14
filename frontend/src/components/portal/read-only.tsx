import type { ReportCard } from "@/lib/api/types";
import { cn } from "@/components/ui/primitives";

function CommentBlock({ label, author, text }: { label: string; author?: string | null; text: string | null }) {
  return (
    <div>
      <p className="mb-1 text-xs font-semibold uppercase tracking-wider text-muted">
        {label}
        {author && <span className="font-normal normal-case tracking-normal"> · {author}</span>}
      </p>
      <p className={cn("min-h-12 whitespace-pre-line rounded-input border border-border px-3 py-2 text-sm", !text && "italic text-muted")}>{text || "No comment."}</p>
    </div>
  );
}

/** The two report comments as plain text (portal: nothing is editable). */
export function ReadOnlyComments({ card }: { card: ReportCard }) {
  return (
    <>
      <CommentBlock label="Form teacher's comment" author={card.formTeacher} text={card.formTeacherComment} />
      <CommentBlock label="Principal's comment" text={card.principalComment} />
    </>
  );
}

/** Behaviour ratings (A–E) and clubs, read-only, with the key. Hidden when the API sends none. */
export function ReadOnlyBehaviour({ card }: { card: ReportCard }) {
  const { behaviour, clubs = [], behaviourKey: key = [] } = card;
  if (!Array.isArray(behaviour) || (behaviour.length === 0 && clubs.length === 0)) return null;
  return (
    <section aria-labelledby="behaviour-heading" className="rounded-input border border-border p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h3 id="behaviour-heading" className="text-xs font-semibold uppercase tracking-wider text-muted">
          Behaviour &amp; clubs
        </h3>
        <ul className="flex flex-wrap gap-1.5 text-xs" aria-label="Rating key">
          {key.map((k) => (
            <li key={k.rating} className="rounded-full bg-surface-2 px-2 py-0.5">
              <span className="font-semibold">{k.rating}</span> {k.meaning}
            </li>
          ))}
        </ul>
      </div>
      <ul className="grid gap-x-8 gap-y-1 sm:grid-cols-2">
        {behaviour.map((t) => (
          <li key={t.traitId} className="flex items-center justify-between gap-3 py-0.5 text-sm">
            <span>{t.trait}</span>
            <span className={cn("inline-flex size-7 items-center justify-center rounded-input font-semibold", t.rating ? "bg-brand/10 text-brand" : "text-muted")}>{t.rating ?? "–"}</span>
          </li>
        ))}
      </ul>
      {clubs.length > 0 && (
        <p className="mt-3 text-sm">
          <span className="font-medium">Clubs &amp; activities: </span>
          {clubs.map((c) => (c.rating ? `${c.name} (${c.rating})` : c.name)).join(" · ")}
        </p>
      )}
    </section>
  );
}
