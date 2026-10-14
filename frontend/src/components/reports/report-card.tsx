import Image from "next/image";
import type { ReportCard } from "@/lib/api/types";
import { formatScore } from "@/lib/assessment/grid";
import { formatDay } from "@/lib/validation/academics";
import { ageOn } from "@/lib/validation/students";
import { formatAverage, ordinal } from "@/lib/reports/rules";
import { cn } from "@/components/ui/primitives";
import { PhotoAvatar } from "@/components/media/photo-avatar";

function Bio({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-[0.6875rem] font-semibold uppercase tracking-wider text-muted">{label}</dt>
      <dd className="truncate text-sm font-medium">{value ?? "—"}</dd>
    </div>
  );
}

function Figure({ label, value, sub }: { label: string; value: React.ReactNode; sub?: React.ReactNode }) {
  return (
    <div className="rounded-input border border-border px-3 py-2 text-center">
      <p className="text-[0.6875rem] font-semibold uppercase tracking-wider text-muted">{label}</p>
      <p className="tabular font-display text-xl font-semibold">{value}</p>
      {sub && <p className="text-xs text-muted">{sub}</p>}
    </div>
  );
}

/**
 * The report card in the school's layout (crest, biodata, subject table, class stats, attendance,
 * comments, grading key, next term). Drafts carry a watermark: figures are live until published.
 * `comments` slots in the editors (or read-only text) from the page.
 */
export function ReportCardView({ card, comments }: { card: ReportCard; comments: React.ReactNode }) {
  const draft = card.status === "Draft";
  return (
    <article className="relative overflow-hidden rounded-card border border-border bg-surface shadow-low" aria-label={`Report card for ${card.studentName}`}>
      {draft && (
        <div aria-hidden className="pointer-events-none absolute inset-0 z-10 flex items-center justify-center overflow-hidden">
          <span className="-rotate-[28deg] whitespace-nowrap font-display text-5xl font-bold tracking-widest text-danger/10 select-none sm:text-7xl">
            DRAFT – figures may change
          </span>
        </div>
      )}

      <header className="brand-panel flex items-center gap-4 px-5 py-4 text-navy-ink sm:px-6">
        <span className="inline-flex size-14 shrink-0 items-center justify-center rounded-full bg-white p-1.5">
          <Image src="/brand/crest.svg" alt="" width={36} height={45} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="font-display text-lg font-semibold uppercase tracking-wide sm:text-xl">Maydel Spring Schools</p>
          <p className="text-sm italic text-gold">The Well Spring of Knowledge</p>
        </div>
        <div className="text-right text-sm">
          <p className="font-semibold">{card.term}</p>
          <p className="text-navy-ink/80">{card.session}</p>
        </div>
      </header>

      <div className="space-y-5 p-5 sm:p-6">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
          <PhotoAvatar
            kind="students"
            id={card.studentId}
            name={card.studentName}
            photoVersion={card.photoVersion}
            alt={card.studentName}
            className="size-24 rounded-card text-2xl"
          />
          <dl className="grid flex-1 grid-cols-2 gap-x-4 gap-y-2 sm:grid-cols-4">
            <div className="col-span-2">
              <Bio label="Name" value={card.studentName} />
            </div>
            <Bio label="Admission no." value={<span className="font-mono">{card.admissionNumber}</span>} />
            <Bio label="Class" value={card.classRoom} />
            <Bio label="Sex" value={card.sex} />
            <Bio label="Age" value={card.dateOfBirth ? ageOn(card.dateOfBirth, card.termEndsOn) : null} />
            <Bio label="House" value={card.house} />
            <Bio label="Status" value={draft ? "Draft" : `Published ${card.publishedAt ? formatDay(card.publishedAt.slice(0, 10)) : ""}`} />
          </dl>
        </div>

        <div className="overflow-x-auto rounded-input border border-border">
          <table className="w-full min-w-[40rem] text-sm">
            <caption className="sr-only">Subject results</caption>
            <thead className="bg-surface-2 text-[0.6875rem] uppercase tracking-wide text-muted">
              <tr>
                <th scope="col" className="px-3 py-2 text-left font-semibold">Subject</th>
                {card.components.map((c) => (
                  <th key={c.id} scope="col" className="px-2 py-2 text-center font-semibold">
                    {c.name}
                    <span className="block font-normal">/{formatScore(c.maxScore)}</span>
                  </th>
                ))}
                <th scope="col" className="border-l border-border px-2 py-2 text-center font-semibold">Total</th>
                <th scope="col" className="px-2 py-2 text-center font-semibold">Grade</th>
                <th scope="col" className="px-2 py-2 text-left font-semibold">Remark</th>
                <th scope="col" className="border-l border-border px-2 py-2 text-center font-semibold" title="Class highest">High</th>
                <th scope="col" className="px-2 py-2 text-center font-semibold" title="Class lowest">Low</th>
                <th scope="col" className="px-2 py-2 text-center font-semibold" title="Class average">Avg</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {card.subjects.length === 0 && (
                <tr>
                  <td colSpan={card.components.length + 7} className="px-3 py-6 text-center text-muted">
                    No scores entered yet.
                  </td>
                </tr>
              )}
              {card.subjects.map((s) => (
                <tr key={s.subjectId}>
                  <th scope="row" className="px-3 py-2 text-left font-medium">
                    {s.subject}
                    {s.comment && <span className="block text-xs font-normal text-muted">{s.comment}</span>}
                  </th>
                  {card.components.map((c) => (
                    <td key={c.id} className="tabular px-2 py-2 text-center">
                      {formatScore(s.scores.find((x) => x.componentId === c.id)?.score) || <span className="text-muted">–</span>}
                    </td>
                  ))}
                  <td className="tabular border-l border-border px-2 py-2 text-center font-semibold">{formatScore(s.total)}</td>
                  <td className={cn("px-2 py-2 text-center font-semibold", s.grade?.startsWith("F") && "text-danger")}>{s.grade ?? "–"}</td>
                  <td className="px-2 py-2 text-muted">{s.remark ?? ""}</td>
                  <td className="tabular border-l border-border px-2 py-2 text-center text-muted">{formatScore(s.classHighest)}</td>
                  <td className="tabular px-2 py-2 text-center text-muted">{formatScore(s.classLowest)}</td>
                  <td className="tabular px-2 py-2 text-center text-muted">{formatAverage(s.classAverage)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          <Figure label="Average" value={formatAverage(card.average)} />
          <Figure label="Position" value={card.position ? ordinal(card.position) : "—"} sub={`of ${card.classSize}`} />
          <Figure label="Class average" value={formatAverage(card.classAverage)} />
          <Figure
            label="Attendance"
            value={card.daysOpened === null ? "—" : `${card.daysPresent ?? "—"}/${card.daysOpened}`}
            sub={card.daysAbsent !== null ? `${card.daysAbsent} absent` : "Not entered"}
          />
        </div>

        <div className="grid gap-4 md:grid-cols-2">{comments}</div>

        <div className="flex flex-col gap-3 border-t border-border pt-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <p className="mb-1 text-[0.6875rem] font-semibold uppercase tracking-wider text-muted">Grading key</p>
            <ul className="flex flex-wrap gap-1.5">
              {card.gradingKey.map((b) => (
                <li key={b.grade} className="rounded-full bg-surface-2 px-2.5 py-0.5 text-xs">
                  <span className="font-semibold">{b.grade}</span> {formatScore(b.minScore)}–{formatScore(b.maxScore)} · {b.remark}
                </li>
              ))}
            </ul>
          </div>
          <dl className="shrink-0 text-sm sm:text-right">
            <div>
              <dt className="inline text-muted">Term ends: </dt>
              <dd className="inline font-medium">{formatDay(card.termEndsOn)}</dd>
            </div>
            <div>
              <dt className="inline text-muted">Next term begins: </dt>
              <dd className="inline font-medium">{card.nextTermBegins ? formatDay(card.nextTermBegins) : "To be announced"}</dd>
            </div>
          </dl>
        </div>
      </div>
    </article>
  );
}
