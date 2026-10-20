/** Dates on the website, in Abuja time (WAT). */
const day = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", timeZone: "Africa/Lagos" });
const dayShort = new Intl.DateTimeFormat("en-NG", { weekday: "short", day: "numeric", month: "short", timeZone: "Africa/Lagos" });
const time = new Intl.DateTimeFormat("en-NG", { hour: "numeric", minute: "2-digit", hour12: true, timeZone: "Africa/Lagos" });
const monthDay = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", timeZone: "Africa/Lagos" });

/** "19 October 2026" */
export const formatDay = (iso: string) => day.format(new Date(iso));

/** "Sat, 24 Oct · 9:00 am", or a range on one day "… 9:00 am – 12:00 pm", or across days. */
export function formatEventWhen(startsAt: string, endsAt: string | null): string {
  const s = new Date(startsAt);
  const start = `${dayShort.format(s)} · ${time.format(s)}`;
  if (!endsAt) return start;
  const e = new Date(endsAt);
  return dayShort.format(s) === dayShort.format(e) ? `${start} – ${time.format(e)}` : `${start} – ${dayShort.format(e)} · ${time.format(e)}`;
}

/** { month: "OCT", day: "24" } for an event's date tile. */
export function dateTile(iso: string): { month: string; day: string } {
  const [d, m] = monthDay.format(new Date(iso)).split(" ");
  return { day: d, month: (m ?? "").toUpperCase() };
}
