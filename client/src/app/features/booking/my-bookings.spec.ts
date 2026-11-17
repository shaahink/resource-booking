import { Booking } from '../../core/models';
import { groupByDay, isUpcoming } from './my-bookings';

const at = (day: number, hour: number, minute = 0) => new Date(2026, 10, day, hour, minute).toISOString();

const booking = (id: string, start: string, end: string, status: Booking['status'] = 'Confirmed'): Booking =>
  ({ id, start, end, status }) as Booking;

describe('my bookings grouping', () => {
  const now = new Date(2026, 10, 17, 10, 0); // Tue 17 Nov 2026, 10:00 local

  it('groups by start day and labels today and tomorrow', () => {
    const days = groupByDay(
      [
        booking('a', at(17, 14), at(17, 15)),
        booking('b', at(17, 16), at(17, 17)),
        booking('c', at(18, 9), at(18, 10)),
        booking('d', at(20, 9), at(20, 10)),
      ],
      now,
    );

    expect(days.map((d) => d.relative)).toEqual(['Today', 'Tomorrow', null]);
    expect(days.map((d) => d.bookings.map((b) => b.id))).toEqual([['a', 'b'], ['c'], ['d']]);
  });

  it('counts a running booking as upcoming and a cancelled one as not', () => {
    expect(isUpcoming(booking('r', at(17, 9, 30), at(17, 10, 30)), now)).toBe(true);
    expect(isUpcoming(booking('x', at(18, 9), at(18, 10), 'Cancelled'), now)).toBe(false);
  });
});
