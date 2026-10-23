import { ResourceConstraint } from './models';

// Human-readable constraint descriptions, shared by the grid header and the admin table.
// Parsing mirrors the server's leniency: malformed params disable a rule there, so here
// they fall back to a generic label instead of breaking the page.

const DAY_ORDER = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'] as const;
const DAY_LABELS: Record<DayKey, string> = {
  mon: 'Mon',
  tue: 'Tue',
  wed: 'Wed',
  thu: 'Thu',
  fri: 'Fri',
  sat: 'Sat',
  sun: 'Sun',
};

export type DayKey = (typeof DAY_ORDER)[number];

/** OpeningHours params: windows per weekday, times as "HH:mm" Europe/London. */
export type OpeningDays = Partial<Record<DayKey, [string, string][]>>;

/** Parse an OpeningHours paramsJson; null when malformed (the server disables such a rule too). */
export function parseOpeningDays(paramsJson: string): OpeningDays | null {
  try {
    const parsed = JSON.parse(paramsJson) as { days?: unknown };
    return parsed.days && typeof parsed.days === 'object' ? (parsed.days as OpeningDays) : null;
  } catch {
    return null;
  }
}

/** One-liner for a constraint, e.g. "Mon–Fri 08:00–20:00", "max 2 h", "1 day notice". */
export function describeConstraint(constraint: ResourceConstraint): string {
  const params = parseParams(constraint.paramsJson);
  switch (constraint.type) {
    case 'MaxDurationMinutes':
      return params.minutes ? `max ${duration(params.minutes)}` : 'max duration';
    case 'MaxAdvanceDays':
      return params.days ? `up to ${params.days} days ahead` : 'advance limit';
    case 'MinNoticeMinutes':
      return params.minutes ? `${duration(params.minutes)} notice` : 'notice required';
    case 'MaxConcurrentPerUser':
      return params.count ? `max ${params.count} overlapping per user` : 'overlap limit per user';
    case 'MaxActiveBookingsPerUser':
      return params.count ? `max ${params.count} upcoming per user` : 'upcoming limit per user';
    case 'OpeningHours':
      return openingHoursLabel(parseOpeningDays(constraint.paramsJson));
  }
}

function parseParams(paramsJson: string): { minutes?: number; days?: number; count?: number } {
  try {
    const parsed = JSON.parse(paramsJson) as { minutes?: unknown; days?: unknown; count?: unknown };
    return {
      minutes: typeof parsed.minutes === 'number' ? parsed.minutes : undefined,
      days: typeof parsed.days === 'number' ? parsed.days : undefined,
      count: typeof parsed.count === 'number' ? parsed.count : undefined,
    };
  } catch {
    return {};
  }
}

function duration(minutes: number): string {
  if (minutes % 1440 === 0) return minutes === 1440 ? '1 day' : `${minutes / 1440} days`;
  if (minutes % 60 === 0) return minutes === 60 ? '1 h' : `${minutes / 60} h`;
  return `${minutes} min`;
}

/** Collapse consecutive days sharing a window into ranges: "Mon–Fri 08:00–20:00; Sat 09:00–12:00". */
function openingHoursLabel(days: OpeningDays | null): string {
  if (!days) return 'opening hours';

  const groups: { from: DayKey; to: DayKey; text: string }[] = [];
  for (const day of DAY_ORDER) {
    const windows = days[day];
    if (!windows || windows.length === 0) continue;
    const text = windows.map(([open, close]) => `${open}–${close}`).join(', ');
    const last = groups[groups.length - 1];
    if (last && last.text === text && DAY_ORDER.indexOf(day) === DAY_ORDER.indexOf(last.to) + 1) {
      last.to = day;
    } else {
      groups.push({ from: day, to: day, text });
    }
  }

  if (groups.length === 0) return 'closed';
  return groups
    .map((g) => `${g.from === g.to ? DAY_LABELS[g.from] : `${DAY_LABELS[g.from]}–${DAY_LABELS[g.to]}`} ${g.text}`)
    .join('; ');
}
