using Api.Domain;
using Api.Services;
using Xunit;

namespace Api.Tests.Unit;

public class BookingRulesTests
{
    // Monday 19 Oct 2026, 09:00 Europe/London (BST, UTC+1).
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T08:00:00Z");

    private readonly BookingRulesService _rules = new();

    private static Resource Room(int capacity = 1, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test room",
        Type = capacity == 1 ? ResourceType.Room : ResourceType.Catering,
        Capacity = capacity,
        IsActive = isActive,
    };

    private static User Alice { get; } = new() { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@test" };
    private static User Bob { get; } = new() { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@test" };

    /// <summary>Tomorrow (Tue) at the given London hour — inside any sane business day.</summary>
    private static DateTimeOffset Tomorrow(double hourLocal) =>
        DateTimeOffset.Parse("2026-10-20T00:00:00+01:00").AddHours(hourLocal);

    private static Booking Existing(Resource resource, User user, DateTimeOffset start, DateTimeOffset end,
        int quantity = 1, BookingStatus status = BookingStatus.Confirmed) => new()
        {
            Id = Guid.NewGuid(),
            ResourceId = resource.Id,
            UserId = user.Id,
            Start = start,
            End = end,
            Quantity = quantity,
            Status = status,
        };

    private static ResourceConstraint Constraint(ConstraintType type, string paramsJson) =>
        new() { Type = type, ParamsJson = paramsJson };

    private BookingContext Context(
        Resource? resource = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        int quantity = 1,
        IReadOnlyList<ResourceConstraint>? constraints = null,
        IReadOnlyList<Booking>? overlapping = null,
        IReadOnlyList<Booking>? usersUpcoming = null,
        User? user = null) =>
        new(
            resource ?? Room(),
            constraints ?? [],
            user ?? Alice,
            start ?? Tomorrow(10),
            end ?? Tomorrow(11),
            quantity,
            overlapping ?? [],
            usersUpcoming ?? [],
            Now);

    private static string[] Codes(IReadOnlyList<RuleViolation> violations) =>
        violations.Select(v => v.Code).OrderBy(c => c).ToArray();

    // ── structural ──────────────────────────────────────────────────────

    [Fact]
    public void Valid_booking_passes()
    {
        Assert.Empty(_rules.Validate(Context()));
    }

    [Theory]
    [InlineData(0)]    // end == start
    [InlineData(-30)]  // end before start
    public void Inverted_or_empty_range_is_invalid(int endOffsetMinutes)
    {
        var start = Tomorrow(10);
        var result = _rules.Validate(Context(start: start, end: start.AddMinutes(endOffsetMinutes)));
        Assert.Contains(RuleCodes.InvalidRange, Codes(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_quantity_is_invalid(int quantity)
    {
        var result = _rules.Validate(Context(resource: Room(capacity: 40), quantity: quantity));
        Assert.Contains(RuleCodes.QuantityInvalid, Codes(result));
    }

    [Fact]
    public void Exclusive_resource_requires_quantity_of_one()
    {
        var result = _rules.Validate(Context(quantity: 2));
        Assert.Contains(RuleCodes.QuantityInvalid, Codes(result));
    }

    [Fact]
    public void Inactive_resource_is_rejected()
    {
        var result = _rules.Validate(Context(resource: Room(isActive: false)));
        Assert.Equal([RuleCodes.InactiveResource], Codes(result));
    }

    // ── overlap & capacity ──────────────────────────────────────────────

    [Fact]
    public void Overlapping_exclusive_booking_conflicts()
    {
        var room = Room();
        var existing = Existing(room, Bob, Tomorrow(10.5), Tomorrow(11.5));
        var result = _rules.Validate(Context(resource: room, overlapping: [existing]));
        Assert.Equal([RuleCodes.OverlapConflict], Codes(result));
    }

    [Fact]
    public void Back_to_back_bookings_are_allowed()
    {
        var room = Room();
        var before = Existing(room, Bob, Tomorrow(9), Tomorrow(10));   // ends exactly at our start
        var after = Existing(room, Bob, Tomorrow(11), Tomorrow(12));   // starts exactly at our end
        var result = _rules.Validate(Context(resource: room, overlapping: [before, after]));
        Assert.Empty(result);
    }

    [Fact]
    public void Cancelled_bookings_never_count()
    {
        var room = Room();
        var cancelled = Existing(room, Bob, Tomorrow(10), Tomorrow(11), status: BookingStatus.Cancelled);
        var result = _rules.Validate(Context(resource: room, overlapping: [cancelled]));
        Assert.Empty(result);
    }

    [Fact]
    public void Pooled_resource_at_exact_capacity_passes()
    {
        var catering = Room(capacity: 40);
        var existing = Existing(catering, Bob, Tomorrow(10), Tomorrow(11), quantity: 30);
        var result = _rules.Validate(Context(resource: catering, overlapping: [existing], quantity: 10));
        Assert.Empty(result);
    }

    [Fact]
    public void Pooled_resource_one_unit_over_capacity_fails()
    {
        var catering = Room(capacity: 40);
        var existing = Existing(catering, Bob, Tomorrow(10), Tomorrow(11), quantity: 30);
        var result = _rules.Validate(Context(resource: catering, overlapping: [existing], quantity: 11));
        Assert.Equal([RuleCodes.CapacityExceeded], Codes(result));
        Assert.Contains("10 of 40", result.Single().Message);
    }

    // ── typed constraints ───────────────────────────────────────────────

    [Theory]
    [InlineData(120, 120, false)] // at the limit — allowed
    [InlineData(120, 121, true)]  // one minute over — rejected
    public void Max_duration_boundary(int limitMinutes, int bookingMinutes, bool expectViolation)
    {
        var start = Tomorrow(9);
        var result = _rules.Validate(Context(
            start: start,
            end: start.AddMinutes(bookingMinutes),
            constraints: [Constraint(ConstraintType.MaxDurationMinutes, $"{{\"minutes\":{limitMinutes}}}")]));
        Assert.Equal(expectViolation, Codes(result).Contains(RuleCodes.MaxDurationExceeded));
    }

    [Fact]
    public void Booking_beyond_max_advance_window_fails()
    {
        var result = _rules.Validate(Context(
            start: Now.AddDays(61),
            end: Now.AddDays(61).AddHours(1),
            constraints: [Constraint(ConstraintType.MaxAdvanceDays, """{"days":60}""")]));
        Assert.Equal([RuleCodes.TooFarInAdvance], Codes(result));
    }

    [Fact]
    public void Booking_within_max_advance_window_passes()
    {
        var result = _rules.Validate(Context(
            start: Now.AddDays(59),
            end: Now.AddDays(59).AddHours(1),
            constraints: [Constraint(ConstraintType.MaxAdvanceDays, """{"days":60}""")]));
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(30, true)]   // starts in 30 min, needs 60 — rejected
    [InlineData(90, false)]  // starts in 90 min — fine
    public void Min_notice_boundary(int startsInMinutes, bool expectViolation)
    {
        var result = _rules.Validate(Context(
            start: Now.AddMinutes(startsInMinutes),
            end: Now.AddMinutes(startsInMinutes + 30),
            constraints: [Constraint(ConstraintType.MinNoticeMinutes, """{"minutes":60}""")]));
        Assert.Equal(expectViolation, Codes(result).Contains(RuleCodes.InsufficientNotice));
    }

    private const string Weekdays8To20 =
        """{"days":{"mon":[["08:00","20:00"]],"tue":[["08:00","20:00"]],"wed":[["08:00","20:00"]],"thu":[["08:00","20:00"]],"fri":[["08:00","20:00"]]}}""";

    [Fact]
    public void Booking_inside_opening_hours_passes()
    {
        var result = _rules.Validate(Context(
            constraints: [Constraint(ConstraintType.OpeningHours, Weekdays8To20)]));
        Assert.Empty(result);
    }

    [Fact]
    public void Booking_filling_the_entire_window_passes()
    {
        var result = _rules.Validate(Context(
            start: Tomorrow(8),
            end: Tomorrow(20),
            constraints: [Constraint(ConstraintType.OpeningHours, Weekdays8To20)]));
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(7, 8)]    // before opening
    [InlineData(19, 21)]  // crosses closing
    public void Booking_outside_opening_hours_fails(int startHour, int endHour)
    {
        var result = _rules.Validate(Context(
            start: Tomorrow(startHour),
            end: Tomorrow(endHour),
            constraints: [Constraint(ConstraintType.OpeningHours, Weekdays8To20)]));
        Assert.Equal([RuleCodes.OutsideOpeningHours], Codes(result));
    }

    [Fact]
    public void Booking_on_a_closed_day_fails()
    {
        var saturday = DateTimeOffset.Parse("2026-10-24T10:00:00+01:00");
        var result = _rules.Validate(Context(
            start: saturday,
            end: saturday.AddHours(1),
            constraints: [Constraint(ConstraintType.OpeningHours, Weekdays8To20)]));
        Assert.Equal([RuleCodes.OutsideOpeningHours], Codes(result));
    }

    [Fact]
    public void Opening_hours_use_london_local_time_across_dst()
    {
        // Monday 30 Mar 2026, the day after clocks go forward: 08:00 London == 07:00 UTC.
        // A naive UTC check would read 07:00 and reject it.
        var result = _rules.Validate(Context(
            start: DateTimeOffset.Parse("2026-03-30T07:00:00Z"),
            end: DateTimeOffset.Parse("2026-03-30T08:00:00Z"),
            constraints: [Constraint(ConstraintType.OpeningHours, Weekdays8To20)]));
        Assert.Empty(result);
    }

    [Fact]
    public void Max_concurrent_per_user_counts_only_own_overlaps()
    {
        var catering = Room(capacity: 40);
        var constraint = Constraint(ConstraintType.MaxConcurrentPerUser, """{"count":1}""");

        // Bob's overlapping booking does not limit Alice…
        var bobs = Existing(catering, Bob, Tomorrow(10), Tomorrow(11), quantity: 5);
        Assert.Empty(_rules.Validate(Context(resource: catering, constraints: [constraint], overlapping: [bobs], quantity: 5)));

        // …but Alice's own overlapping booking does.
        var alices = Existing(catering, Alice, Tomorrow(10), Tomorrow(11), quantity: 5);
        var result = _rules.Validate(Context(resource: catering, constraints: [constraint], overlapping: [alices], quantity: 5));
        Assert.Equal([RuleCodes.MaxConcurrentPerUser], Codes(result));
    }

    [Fact]
    public void Max_active_bookings_counts_only_upcoming_confirmed()
    {
        var car = Room();
        var constraint = Constraint(ConstraintType.MaxActiveBookingsPerUser, """{"count":2}""");

        var past = Existing(car, Alice, Now.AddDays(-2), Now.AddDays(-2).AddHours(4));
        var cancelled = Existing(car, Alice, Tomorrow(9), Tomorrow(10), status: BookingStatus.Cancelled);
        var upcoming1 = Existing(car, Alice, Now.AddDays(2), Now.AddDays(2).AddHours(2));

        // past + cancelled + 1 upcoming → this booking is the 2nd active: allowed.
        Assert.Empty(_rules.Validate(Context(
            resource: car, constraints: [constraint],
            usersUpcoming: [past, cancelled, upcoming1])));

        // a 2nd real upcoming → this would be the 3rd: rejected.
        var upcoming2 = Existing(car, Alice, Now.AddDays(3), Now.AddDays(3).AddHours(2));
        var result = _rules.Validate(Context(
            resource: car, constraints: [constraint],
            usersUpcoming: [upcoming1, upcoming2]));
        Assert.Equal([RuleCodes.MaxActiveBookings], Codes(result));
    }

    // ── engine behaviour ────────────────────────────────────────────────

    [Fact]
    public void All_violations_are_returned_not_just_the_first()
    {
        var room = Room();
        var existing = Existing(room, Bob, Tomorrow(6), Tomorrow(8));
        var result = _rules.Validate(Context(
            resource: room,
            start: Tomorrow(5),
            end: Tomorrow(7.5), // overlaps existing, outside hours, 150 min long
            overlapping: [existing],
            constraints:
            [
                Constraint(ConstraintType.OpeningHours, Weekdays8To20),
                Constraint(ConstraintType.MaxDurationMinutes, """{"minutes":120}"""),
            ]));

        Assert.Equal(
            [RuleCodes.MaxDurationExceeded, RuleCodes.OutsideOpeningHours, RuleCodes.OverlapConflict],
            Codes(result));
    }

    [Fact]
    public void Malformed_constraint_params_disable_that_constraint_only()
    {
        var result = _rules.Validate(Context(
            constraints:
            [
                Constraint(ConstraintType.MaxDurationMinutes, "not json at all"),
                Constraint(ConstraintType.MinNoticeMinutes, """{"minutes":999999}"""),
            ]));

        // The broken MaxDuration is skipped; the valid MinNotice still enforces.
        Assert.Equal([RuleCodes.InsufficientNotice], Codes(result));
    }
}
