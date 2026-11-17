import { Booking } from '../../core/models';
import { MS_PER_DAY } from '../../core/settings';

// Pure grouping for the My bookings page — free of Angular so it is trivially testable.

export interface BookingDay {
  /** Local midnight of the day. */
  date: Date;
  /** "Today" / "Tomorrow" / "Yesterday", or null to show the date itself. */
  relative: string | null;
  bookings: Booking[];
}

const RELATIVE_DAYS = new Map([
  [-1, 'Yesterday'],
  [0, 'Today'],
  [1, 'Tomorrow'],
]);

/** Bookings grouped by the local day they start, in the order given. */
export function groupByDay(bookings: readonly Booking[], now: Date): BookingDay[] {
  const today = startOfDay(now).getTime();
  const days: BookingDay[] = [];

  for (const booking of bookings) {
    const date = startOfDay(new Date(booking.start));
    let day = days.at(-1);
    if (!day || day.date.getTime() !== date.getTime()) {
      const offset = Math.round((date.getTime() - today) / MS_PER_DAY);
      day = { date, relative: RELATIVE_DAYS.get(offset) ?? null, bookings: [] };
      days.push(day);
    }
    day.bookings.push(booking);
  }
  return days;
}

/** Still to come: confirmed and not yet ended. */
export function isUpcoming(booking: Booking, now: Date): boolean {
  return booking.status === 'Confirmed' && new Date(booking.end) > now;
}

function startOfDay(date: Date): Date {
  const day = new Date(date);
  day.setHours(0, 0, 0, 0);
  return day;
}
