import { AvailabilityBooking } from '../../core/models';

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
export function slotsForDay(day: Date, fromHour: number, toHour: number, stepMinutes = 30): GridSlot[] {
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

/** Total booked quantity covering a slot (confirmed bookings only arrive from the API). */
export function usedQuantity(bookings: AvailabilityBooking[], slot: GridSlot): number {
  return bookings
    .filter((b) => overlaps(new Date(b.start), new Date(b.end), slot.start, slot.end))
    .reduce((sum, b) => sum + b.quantity, 0);
}

/** The bookings covering a slot (for ownership highlighting and tooltips). */
export function bookingsInSlot(bookings: AvailabilityBooking[], slot: GridSlot): AvailabilityBooking[] {
  return bookings.filter((b) => overlaps(new Date(b.start), new Date(b.end), slot.start, slot.end));
}
