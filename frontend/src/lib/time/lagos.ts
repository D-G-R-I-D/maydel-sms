/** Africa/Lagos is UTC+1 all year (no DST). The API stores instants; staff pick Lagos dates and times. */

const LAGOS = "Africa/Lagos";
const dateTime = new Intl.DateTimeFormat("en-NG", { weekday: "short", day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: LAGOS });

/** A Lagos date ("2026-10-24") and time ("09:00") → ISO with +01:00. */
export function lagosIso(date: string, time: string): string {
  return `${date}T${time}:00+01:00`;
}

/** The Lagos date and time of an instant, for prefilling date/time inputs. */
export function toLagosParts(iso: string): { date: string; time: string } {
  const s = new Date(new Date(iso).getTime() + 60 * 60 * 1000).toISOString();
  return { date: s.slice(0, 10), time: s.slice(11, 16) };
}

/** "Sat, 24 Oct 2026, 09:00" (Lagos). */
export function formatLagos(iso: string): string {
  return dateTime.format(new Date(iso));
}
