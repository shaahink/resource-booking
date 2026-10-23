import { peakQuantity } from '../../core/capacity';
import { DayKey, OpeningDays, parseOpeningDays } from '../../core/constraints';
import { AvailabilityBooking, ResourceConstraint } from '../../core/models';

// Pure slot math for the availability grid — kept free of Angular so it is trivially testable.

export interface GridSlot {
  start: Date;
  end: Date;
}

/** Monday 00:00 of the week containing `anchor` (local time). */
export function startOfWeek(anchor: Date): Date {
  const date = new Date(anchor);
  date.setHours(0, 0, 0, 0);
  const shift = (date.getDay() + 6) % 7; // Mon=0 … Sun=6
  date.setDate(date.getDate() - shift);
  return date;
}

/** The seven days of `anchor`'s week, Monday first. */
export function weekDays(anchor: Date): Date[] {
  const monday = startOfWeek(anchor);
  return Array.from({ length: 7 }, (_, i) => {
    const day = new Date(monday);
    day.setDate(monday.getDate() + i);
    return day;
  });
}

/** Half-hour slots for one day between fromHour (inclusive) and toHour (exclusive). */
export function slotsForDay(
  day: Date,
  fromHour: number,
  toHour: number,
  stepMinutes = 30,
): GridSlot[] {
  const slots: GridSlot[] = [];
  const cursor = new Date(day);
  cursor.setHours(fromHour, 0, 0, 0);
  const dayEnd = new Date(day);
  dayEnd.setHours(toHour, 0, 0, 0);
  while (cursor < dayEnd) {
    const end = new Date(cursor.getTime() + stepMinutes * 60_000);
    slots.push({ start: new Date(cursor), end });
    cursor.setTime(end.getTime());
  }
  return slots;
}

/** Strict interval overlap — back-to-back ranges do NOT overlap (matches the server). */
export function overlaps(aStart: Date, aEnd: Date, bStart: Date, bEnd: Date): boolean {
  return aStart < bEnd && aEnd > bStart;
}

/** Peak quantity in use at any instant of a slot (confirmed bookings only arrive from the API). */
export function usedQuantity(bookings: AvailabilityBooking[], slot: GridSlot): number {
  return peakQuantity(bookings, slot.start, slot.end);
}

/** The bookings covering a slot (for ownership highlighting and tooltips). */
export function bookingsInSlot(
  bookings: AvailabilityBooking[],
  slot: GridSlot,
): AvailabilityBooking[] {
  return bookings.filter((b) => overlaps(new Date(b.start), new Date(b.end), slot.start, slot.end));
}

// ── Opening hours → closed cells ────────────────────────────────────────
// Opening hours are defined in Europe/London (see docs/contracts.md); the viewer may be
// anywhere, so a slot's wall-clock is read in London time before checking the windows.
// This mirrors the server's OpeningHours rule — the grid shows what the API will enforce.

const londonClock = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/London',
  weekday: 'short',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

function londonDayAndMinutes(instant: Date): { day: DayKey; minutes: number } {
  const parts = londonClock.formatToParts(instant);
  const part = (type: string): string => parts.find((p) => p.type === type)?.value ?? '';
  return {
    day: part('weekday').toLowerCase() as DayKey,
    minutes: Number(part('hour')) * 60 + Number(part('minute')),
  };
}

/** A resource's opening windows, or null when it has no OpeningHours constraint (always open). */
export function openingDays(constraints: ResourceConstraint[]): OpeningDays | null {
  const constraint = constraints.find((c) => c.type === 'OpeningHours');
  return constraint ? parseOpeningDays(constraint.paramsJson) : null;
}

/** True when no opening window fully contains the slot. */
export function isClosed(slot: GridSlot, days: OpeningDays | null): boolean {
  if (!days) return false;
  const start = londonDayAndMinutes(slot.start);
  const endMinutes = start.minutes + (slot.end.getTime() - slot.start.getTime()) / 60_000;
  const windows = days[start.day] ?? [];
  return !windows.some(
    ([open, close]) => toMinutes(open) <= start.minutes && endMinutes <= toMinutes(close),
  );
}

function toMinutes(hhmm: string): number {
  const [hours, minutes] = hhmm.split(':').map(Number);
  return (hours || 0) * 60 + (minutes || 0);
}
