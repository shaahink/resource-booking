import { Booking, Resource, User } from './models';

// Mirror of Api/Data/SeedData.cs — same ids, same shape, smaller booking set.
// Keeping ids identical means deep links built against mocks keep working on the real API.

export const MOCK_USERS: User[] = [
  { id: '00000000-0000-0000-0000-000000000001', name: 'Priya Sharma', email: 'priya@demo.local', role: 'Admin' },
  { id: '00000000-0000-0000-0000-000000000002', name: 'Tom Hale', email: 'tom@demo.local', role: 'User' },
  { id: '00000000-0000-0000-0000-000000000003', name: 'Maya Chen', email: 'maya@demo.local', role: 'User' },
  { id: '00000000-0000-0000-0000-000000000004', name: 'Jonas Weber', email: 'jonas@demo.local', role: 'User' },
  { id: '00000000-0000-0000-0000-000000000005', name: 'Sofia Rossi', email: 'sofia@demo.local', role: 'User' },
];

const openingHoursWeekdays = JSON.stringify({
  days: {
    mon: [['08:00', '20:00']],
    tue: [['08:00', '20:00']],
    wed: [['08:00', '20:00']],
    thu: [['08:00', '20:00']],
    fri: [['08:00', '20:00']],
  },
});

export const MOCK_RESOURCES: Resource[] = [
  {
    id: '10000000-0000-0000-0000-000000000001',
    name: 'Boardroom',
    type: 'Room',
    capacity: 1,
    seats: 14,
    location: '1st floor',
    description: 'Large meeting room with video conferencing and whiteboard wall.',
    isActive: true,
    constraints: [
      { id: 'c1', type: 'OpeningHours', paramsJson: openingHoursWeekdays },
      { id: 'c2', type: 'MaxAdvanceDays', paramsJson: '{"days":60}' },
    ],
  },
  {
    id: '10000000-0000-0000-0000-000000000002',
    name: 'Huddle A',
    type: 'Room',
    capacity: 1,
    seats: 4,
    location: '2nd floor',
    description: 'Small huddle room for quick syncs.',
    isActive: true,
    constraints: [
      { id: 'c3', type: 'OpeningHours', paramsJson: openingHoursWeekdays },
      { id: 'c4', type: 'MaxDurationMinutes', paramsJson: '{"minutes":120}' },
    ],
  },
  {
    id: '10000000-0000-0000-0000-000000000003',
    name: 'Huddle B',
    type: 'Room',
    capacity: 1,
    seats: 4,
    location: '2nd floor',
    description: 'Small huddle room next to the kitchen.',
    isActive: true,
    constraints: [
      { id: 'c5', type: 'OpeningHours', paramsJson: openingHoursWeekdays },
      { id: 'c6', type: 'MaxDurationMinutes', paramsJson: '{"minutes":120}' },
    ],
  },
  {
    id: '10000000-0000-0000-0000-000000000004',
    name: 'Workshop Room',
    type: 'Room',
    capacity: 1,
    seats: 20,
    location: 'Ground floor',
    description: 'Training and workshop space with movable desks.',
    isActive: true,
    constraints: [{ id: 'c7', type: 'OpeningHours', paramsJson: openingHoursWeekdays }],
  },
  {
    id: '10000000-0000-0000-0000-000000000005',
    name: 'Pool Car 1 (EV)',
    type: 'Car',
    capacity: 1,
    seats: null,
    location: 'Car park B2',
    description: 'Electric hatchback, ~250 km range.',
    isActive: true,
    constraints: [
      { id: 'c8', type: 'MaxActiveBookingsPerUser', paramsJson: '{"count":2}' },
      { id: 'c9', type: 'MinNoticeMinutes', paramsJson: '{"minutes":60}' },
    ],
  },
  {
    id: '10000000-0000-0000-0000-000000000006',
    name: 'Pool Car 2 (Estate)',
    type: 'Car',
    capacity: 1,
    seats: null,
    location: 'Car park B2',
    description: 'Estate car for equipment transport.',
    isActive: true,
    constraints: [
      { id: 'c10', type: 'MaxActiveBookingsPerUser', paramsJson: '{"count":2}' },
      { id: 'c11', type: 'MinNoticeMinutes', paramsJson: '{"minutes":60}' },
    ],
  },
  {
    id: '10000000-0000-0000-0000-000000000007',
    name: 'Lunch Catering',
    type: 'Catering',
    capacity: 40,
    seats: null,
    location: 'Kitchen',
    description: 'Catered lunch portions; book the number of meals you need.',
    isActive: true,
    constraints: [{ id: 'c12', type: 'MinNoticeMinutes', paramsJson: '{"minutes":1440}' }],
  },
];

/** A believable spread of bookings across today ± a week, regenerated at load so the grid is never stale. */
export function generateMockBookings(): Booking[] {
  const bookings: Booking[] = [];
  let n = 0;

  const at = (dayOffset: number, hour: number, minute = 0): Date => {
    const d = new Date();
    d.setDate(d.getDate() + dayOffset);
    d.setHours(hour, minute, 0, 0);
    return d;
  };

  const add = (
    resourceIndex: number,
    userIndex: number,
    dayOffset: number,
    hour: number,
    durationMinutes: number,
    quantity = 1,
  ): void => {
    const resource = MOCK_RESOURCES[resourceIndex];
    const user = MOCK_USERS[userIndex];
    const start = at(dayOffset, hour);
    const end = new Date(start.getTime() + durationMinutes * 60_000);
    bookings.push({
      id: `b${++n}`,
      resourceId: resource.id,
      resourceName: resource.name,
      resourceType: resource.type,
      userId: user.id,
      userName: user.name,
      start: start.toISOString(),
      end: end.toISOString(),
      quantity,
      status: 'Confirmed',
      notes: null,
      createdAt: at(dayOffset - 2, 9).toISOString(),
    });
  };

  for (const dayOffset of [-7, -6, -5, -2, -1]) {
    add(0, 1, dayOffset, 9, 60);
    add(0, 2, dayOffset, 11, 90);
    add(1, 3, dayOffset, 10, 30);
    add(2, 4, dayOffset, 14, 60);
    add(3, 1, dayOffset, 13, 120);
    add(6, 2, dayOffset, 12, 90, 15);
  }
  add(0, 2, 0, 10, 60);
  add(0, 3, 0, 14, 90);
  add(1, 1, 0, 9, 30);
  add(4, 4, 0, 8, 240);
  add(6, 1, 1, 12, 90, 20);
  add(0, 1, 1, 11, 60);
  add(2, 2, 1, 15, 60);
  add(3, 3, 2, 9, 180);

  return bookings;
}
