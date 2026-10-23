import { OpeningDays } from '../../core/constraints';
import { AvailabilityBooking } from '../../core/models';
import {
  bookingsInSlot,
  isClosed,
  overlaps,
  slotsForDay,
  startOfWeek,
  usedQuantity,
  weekDays,
} from './grid';

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

  describe('capacity within a slot (peak concurrent quantity, not a sum)', () => {
    const at = (hour: number, minute: number) => new Date(2026, 9, 21, hour, minute);
    const span = (from: Date, to: Date, quantity: number): AvailabilityBooking => ({
      id: 'b',
      start: from.toISOString(),
      end: to.toISOString(),
      quantity,
      mine: false,
    });
    const slot = { start: at(14, 0), end: at(14, 30) };
    const capacity = 40;

    it('does not stack back-to-back bookings inside one slot', () => {
      const used = usedQuantity(
        [span(at(14, 0), at(14, 15), 20), span(at(14, 15), at(14, 30), 20)],
        slot,
      );
      expect(used).toBe(20);
      expect(used >= capacity).toBe(false);
    });

    it('still sums bookings that overlap inside the slot', () => {
      const used = usedQuantity(
        [span(at(14, 0), at(14, 20), 20), span(at(14, 10), at(14, 30), 20)],
        slot,
      );
      expect(used).toBe(capacity);
    });

    it('ignores the part of a booking that falls outside the slot', () => {
      const used = usedQuantity(
        [span(at(13, 0), at(14, 10), 30), span(at(14, 10), at(15, 0), 5)],
        slot,
      );
      expect(used).toBe(30);
    });
  });

  it('returns the bookings covering a slot', () => {
    const slot = { start: new Date(2026, 9, 21, 10), end: new Date(2026, 9, 21, 10, 30) };
    expect(bookingsInSlot([booking(9, 11), booking(10, 11), booking(11, 12)], slot).length).toBe(2);
  });

  describe('closed cells (opening hours are Europe/London)', () => {
    // Weekdays 08:00–20:00 London. UTC instants keep the tests timezone-independent:
    // Wed 21 Oct 2026 is BST, so 08:00 London == 07:00Z.
    const window: [string, string][] = [['08:00', '20:00']];
    const weekdays: OpeningDays = {
      mon: window,
      tue: window,
      wed: window,
      thu: window,
      fri: window,
    };
    const slotAt = (iso: string) => {
      const start = new Date(iso);
      return { start, end: new Date(start.getTime() + 30 * 60_000) };
    };

    it('marks slots outside the windows as closed, boundaries exact', () => {
      expect(isClosed(slotAt('2026-10-21T06:30:00Z'), weekdays)).toBe(true); // 07:30 London — before opening
      expect(isClosed(slotAt('2026-10-21T07:00:00Z'), weekdays)).toBe(false); // 08:00 — opens
      expect(isClosed(slotAt('2026-10-21T18:30:00Z'), weekdays)).toBe(false); // 19:30–20:00 — last slot
      expect(isClosed(slotAt('2026-10-21T19:00:00Z'), weekdays)).toBe(true); // 20:00 — closed
    });

    it('marks whole days without a window as closed', () => {
      expect(isClosed(slotAt('2026-10-24T09:00:00Z'), weekdays)).toBe(true); // Saturday
    });

    it('never closes a resource without opening hours', () => {
      expect(isClosed(slotAt('2026-10-24T02:00:00Z'), null)).toBe(false);
    });
  });
});
