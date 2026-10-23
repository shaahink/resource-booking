import { constraintDraftValid, toConstraintDraft, toParamsJson } from './constraint-draft';

describe('constraint drafts', () => {
  describe('opening hours round trip', () => {
    const roundTrip = (paramsJson: string) =>
      toParamsJson(toConstraintDraft('OpeningHours', paramsJson));

    it('keeps different hours on different days unchanged', () => {
      const params = '{"days":{"mon":[["08:00","20:00"]],"sat":[["09:00","12:00"]]}}';
      expect(toConstraintDraft('OpeningHours', params).raw).toBe(params);
      expect(roundTrip(params)).toBe(params);
    });

    it('keeps a split window day unchanged', () => {
      const params =
        '{"days":{"mon":[["08:00","12:00"],["13:00","18:00"]],"tue":[["08:00","18:00"]]}}';
      expect(toConstraintDraft('OpeningHours', params).raw).toBe(params);
      expect(roundTrip(params)).toBe(params);
    });

    it('edits one shared window across days and writes it back the same', () => {
      const params = '{"days":{"mon":[["07:30","19:00"]],"wed":[["07:30","19:00"]]}}';
      const draft = toConstraintDraft('OpeningHours', params);
      expect(draft.raw).toBeNull();
      expect(draft.weekdays).toEqual({
        mon: true,
        tue: false,
        wed: true,
        thu: false,
        fri: false,
        sat: false,
        sun: false,
      });
      expect([draft.open, draft.close]).toEqual(['07:30', '19:00']);
      expect(toParamsJson(draft)).toBe(params);
    });

    it('keeps unreadable params unchanged', () => {
      expect(roundTrip('not json')).toBe('not json');
      expect(toParamsJson(toConstraintDraft('MaxDurationMinutes', '{"minutes":"two"}'))).toBe(
        '{"minutes":"two"}',
      );
    });
  });

  describe('numeric params', () => {
    const withValue = (value: number | null) => ({
      ...toConstraintDraft('MaxDurationMinutes', null),
      value,
    });

    it('round-trips the value', () => {
      expect(toParamsJson(toConstraintDraft('MaxAdvanceDays', '{"days":14}'))).toBe('{"days":14}');
    });

    it('requires a whole number from 1 to the server cap', () => {
      expect(constraintDraftValid(withValue(null))).toBe(false); // cleared input
      expect(constraintDraftValid(withValue(0))).toBe(false);
      expect(constraintDraftValid(withValue(1.5))).toBe(false);
      expect(constraintDraftValid(withValue(1))).toBe(true);
      expect(constraintDraftValid(withValue(525_600))).toBe(true);
      expect(constraintDraftValid(withValue(525_601))).toBe(false);
    });

    it('applies each type its own cap', () => {
      const days = toConstraintDraft('MaxAdvanceDays', '{"days":3651}');
      const count = toConstraintDraft('MaxConcurrentPerUser', '{"count":1001}');
      expect(constraintDraftValid(days)).toBe(false);
      expect(constraintDraftValid(count)).toBe(false);
      expect(constraintDraftValid({ ...days, value: 3_650 })).toBe(true);
      expect(constraintDraftValid({ ...count, value: 1_000 })).toBe(true);
    });
  });
});
