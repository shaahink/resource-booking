import { AvailabilityBooking } from '../../core/models';
import { bookingsInSlot, overlaps, slotsForDay, startOfWeek, usedQuantity, weekDays } from './grid';

describe('grid math', () => {
  const wednesday = new Date(2026, 9, 21, 15, 30); // Wed 21 Oct 2026

  it('finds Monday as the start of the week', () => {
    const monday = startOfWeek(wednesday);
    expect(monday.getDay()).toBe(1);
    expect(monday.getDate()).toBe(19);
    expect(monday.getHours()).toBe(0);
  });

  it('keeps a Monday anchored to itself and handles Sunday as week end', () => {
    expect(startOfWeek(new Date(2026, 9, 19)).getDate()).toBe(19); // Monday stays
    expect(startOfWeek(new Date(2026, 9, 25)).getDate()).toBe(19); // Sunday → preceding Monday
  });

  it('produces seven days Monday-first', () => {
    const days = weekDays(wednesday);
    expect(days.length).toBe(7);
    expect(days[0].getDay()).toBe(1);
    expect(days[6].getDay()).toBe(0);
  });

  it('builds half-hour slots across the visible day', () => {
    const slots = slotsForDay(wednesday, 7, 21);
    expect(slots.length).toBe(28);
    expect(slots[0].start.getHours()).toBe(7);
    expect(slots[27].end.getHours()).toBe(21);
  });

  it('treats back-to-back ranges as non-overlapping, matching the server', () => {
    const ten = new Date(2026, 9, 21, 10);
    const eleven = new Date(2026, 9, 21, 11);
    const twelve = new Date(2026, 9, 21, 12);
    expect(overlaps(ten, eleven, eleven, twelve)).toBe(false);
    expect(overlaps(ten, twelve, eleven, twelve)).toBe(true);
  });

  const booking = (startHour: number, endHour: number, quantity = 1): AvailabilityBooking => ({
    id: 'b',
    start: new Date(2026, 9, 21, startHour).toISOString(),
    end: new Date(2026, 9, 21, endHour).toISOString(),
    quantity,
    mine: false,
  });

  it('sums quantities of bookings covering a slot', () => {
    const slot = { start: new Date(2026, 9, 21, 12), end: new Date(2026, 9, 21, 12, 30) };
    const used = usedQuantity([booking(11, 13, 10), booking(12, 14, 5), booking(14, 15, 99)], slot);
    expect(used).toBe(15);
  });

  it('returns the bookings covering a slot', () => {
    const slot = { start: new Date(2026, 9, 21, 10), end: new Date(2026, 9, 21, 10, 30) };
    expect(bookingsInSlot([booking(9, 11), booking(10, 11), booking(11, 12)], slot).length).toBe(2);
  });
});
