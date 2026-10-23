import { AvailabilityBooking } from './models';

type Occupancy = Pick<AvailabilityBooking, 'start' | 'end' | 'quantity'>;

/**
 * The most units in use at any one instant of [from, to). Each booking is clipped to the
 * window, then start/end events are swept in time order. At the same instant an end sorts
 * before a start, so back-to-back bookings never stack (matches the server).
 */
export function peakQuantity(bookings: readonly Occupancy[], from: Date, to: Date): number {
  const events: [at: number, delta: number][] = [];
  for (const booking of bookings) {
    const start = Math.max(new Date(booking.start).getTime(), from.getTime());
    const end = Math.min(new Date(booking.end).getTime(), to.getTime());
    if (start < end) events.push([start, booking.quantity], [end, -booking.quantity]);
  }
  events.sort(([atA, deltaA], [atB, deltaB]) => atA - atB || deltaA - deltaB);

  let used = 0;
  let peak = 0;
  for (const [, delta] of events) {
    used += delta;
    peak = Math.max(peak, used);
  }
  return peak;
}
