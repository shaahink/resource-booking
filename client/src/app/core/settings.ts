// The client's fixed numbers, in one place. A value that "mirrors" a server constant must change
// with it — the server stays the authority and rejects anything the two disagree on.

/** One bookable slot — mirrors `BookingRules.SlotMinutes`. */
export const SLOT_MINUTES = 30;

/** Hours the week grid shows, local time: first hour shown, and the hour it stops before. */
export const GRID_START_HOUR = 7;
export const GRID_END_HOUR = 21;

/** Durations the booking dialog offers before the resource's own rules trim the list. */
export const DURATION_CHOICES_MINUTES = [30, 60, 90, 120, 180, 240, 480];
export const DEFAULT_DURATION_MINUTES = 60;

/** The dashboard's range toggle, in days. */
export const DASHBOARD_RANGES_DAYS = [7, 14, 30];
export const DEFAULT_DASHBOARD_RANGE_DAYS = 14;

/** Upper bound for capacity and seats — mirrors `FieldLimits.MaxCapacity`. */
export const MAX_CAPACITY = 10_000;

/** How long a confirmation toast stays up. */
export const TOAST_MS = 4_000;

/** How often the grid re-checks which cells have passed. */
export const CLOCK_TICK_MS = 60_000;

export const MINUTES_PER_HOUR = 60;
export const MINUTES_PER_DAY = 1_440;
export const MS_PER_MINUTE = 60_000;
export const MS_PER_DAY = 86_400_000;
