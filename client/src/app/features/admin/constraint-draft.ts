import { DayKey, OpeningDays, parseOpeningDays } from '../../core/constraints';
import { ConstraintType } from '../../core/models';

// Pure constraint-editor logic for the resources drawer — free of Angular so it is testable.

export const WEEKDAY_KEYS = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'] as const;

export type NumericParam = 'minutes' | 'days' | 'count';

/** Upper bounds the server enforces on each numeric parameter; the lower bound is 1. */
export const PARAM_MAX: Record<NumericParam, number> = {
  minutes: 525_600,
  days: 3_650,
  count: 1_000,
};

const PARAM_DEFAULT: Record<NumericParam, number> = { minutes: 60, days: 30, count: 1 };

/**
 * Editable draft of one constraint: `value` for the numeric types, weekday flags sharing
 * one window for OpeningHours. Params the editor cannot show faithfully (split windows,
 * different hours per day, unreadable JSON) are kept in `raw` and saved back untouched.
 */
export interface ConstraintDraft {
  type: ConstraintType;
  raw: string | null;
  value: number | null;
  weekdays: Partial<Record<DayKey, boolean>>;
  open: string;
  close: string;
}

/** The numeric parameter a constraint type carries, or null for OpeningHours. */
export function numericParam(type: ConstraintType): NumericParam | null {
  switch (type) {
    case 'MaxDurationMinutes':
    case 'MinNoticeMinutes':
      return 'minutes';
    case 'MaxAdvanceDays':
      return 'days';
    case 'MaxConcurrentPerUser':
    case 'MaxActiveBookingsPerUser':
      return 'count';
    case 'OpeningHours':
      return null;
  }
}

/** A whole number from 1 to max. */
export function isWholeInRange(value: number | null, max: number): boolean {
  return value !== null && Number.isInteger(value) && value >= 1 && value <= max;
}

export function toConstraintDraft(
  type: ConstraintType,
  paramsJson: string | null,
): ConstraintDraft {
  const param = numericParam(type);
  const draft: ConstraintDraft = {
    type,
    raw: null,
    value: param ? PARAM_DEFAULT[param] : null,
    weekdays: { mon: true, tue: true, wed: true, thu: true, fri: true },
    open: '08:00',
    close: '18:00',
  };
  if (paramsJson === null) return draft;

  try {
    const fields = param
      ? numericFields(param, paramsJson)
      : openingHoursFields(parseOpeningDays(paramsJson));
    return fields ? { ...draft, ...fields } : { ...draft, raw: paramsJson };
  } catch {
    return { ...draft, raw: paramsJson };
  }
}

export function toParamsJson(draft: ConstraintDraft): string {
  if (draft.raw !== null) return draft.raw;

  const param = numericParam(draft.type);
  if (param) return JSON.stringify({ [param]: draft.value });

  const days: OpeningDays = {};
  for (const key of WEEKDAY_KEYS) {
    if (draft.weekdays[key]) days[key] = [[draft.open, draft.close]];
  }
  return JSON.stringify({ days });
}

export function constraintDraftValid(draft: ConstraintDraft): boolean {
  if (draft.raw !== null) return true;
  const param = numericParam(draft.type);
  return param
    ? isWholeInRange(draft.value, PARAM_MAX[param])
    : draft.open !== '' && draft.open < draft.close;
}

function numericFields(param: NumericParam, paramsJson: string): Partial<ConstraintDraft> | null {
  const value = (JSON.parse(paramsJson) as Partial<Record<NumericParam, unknown>>)[param];
  return typeof value === 'number' ? { value } : null;
}

/** Representable only when every listed day has exactly one window and all windows match. */
function openingHoursFields(days: OpeningDays | null): Partial<ConstraintDraft> | null {
  if (!days) return null;

  const entries = Object.entries(days);
  const [open, close] = entries[0]?.[1]?.[0] ?? [];
  const uniform = entries.every(
    ([day, windows]) =>
      (WEEKDAY_KEYS as readonly string[]).includes(day) &&
      windows?.length === 1 &&
      windows[0][0] === open &&
      windows[0][1] === close,
  );
  if (!uniform) return null;

  const weekdays: Partial<Record<DayKey, boolean>> = {};
  for (const key of WEEKDAY_KEYS) weekdays[key] = key in days;
  return open && close ? { weekdays, open, close } : { weekdays };
}
