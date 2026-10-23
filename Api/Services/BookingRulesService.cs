using Api.Domain;

namespace Api.Services;

/// <summary>
/// THE single choke point for booking validation. Every booking mutation — the form,
/// the AI assistant, anything future — passes through Validate. Returns ALL violations, never just the first.
/// </summary>
public interface IBookingRules
{
    IReadOnlyList<RuleViolation> Validate(BookingContext context);
}

/// <summary>
/// The booking rule engine — the single choke point for every booking mutation.
/// Pure: all data arrives via BookingContext (no EF, no clock, no I/O), so every rule
/// is unit-testable in isolation. Returns ALL violations, never just the first.
///
/// Structural failures (inverted range, bad quantity) suppress the checks that would be
/// meaningless on nonsense input; everything else accumulates.
///
/// A constraint whose params <see cref="ConstraintParams"/> cannot read is skipped rather
/// than blocking all booking of the resource — the validator rejects such params on save,
/// and a bad row from anywhere else should not take the resource offline.
/// </summary>
public sealed class BookingRulesService : IBookingRules
{
    /// <summary>The grid's slot length: the slot that is currently running stays bookable.</summary>
    private static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

    public IReadOnlyList<RuleViolation> Validate(BookingContext context)
    {
        var violations = new List<RuleViolation>();

        if (!context.Resource.IsActive)
            violations.Add(new(RuleCodes.InactiveResource, "This resource is not currently bookable."));

        var structurallySound = true;
        if (context.End <= context.Start)
        {
            violations.Add(new(RuleCodes.InvalidRange, "End must be after start."));
            structurallySound = false;
        }

        if (context.Quantity < 1 || (context.Resource.Capacity == 1 && context.Quantity != 1))
        {
            violations.Add(new(RuleCodes.QuantityInvalid,
                context.Resource.Capacity == 1
                    ? "This resource is booked exclusively — quantity must be 1."
                    : "Quantity must be at least 1."));
            structurallySound = false;
        }

        if (!structurallySound) return violations;

        if (context.End <= context.Now || context.Start < CurrentSlotStart(context.Now))
            violations.Add(new(RuleCodes.InThePast, "This time has already passed."));

        CheckCapacity(context, violations);

        foreach (var constraint in context.Constraints)
        {
            switch (constraint.Type)
            {
                case ConstraintType.MaxDurationMinutes:
                    CheckMaxDuration(context, constraint, violations);
                    break;
                case ConstraintType.MaxAdvanceDays:
                    CheckMaxAdvance(context, constraint, violations);
                    break;
                case ConstraintType.MinNoticeMinutes:
                    CheckMinNotice(context, constraint, violations);
                    break;
                case ConstraintType.OpeningHours:
                    CheckOpeningHours(context, constraint, violations);
                    break;
                case ConstraintType.MaxConcurrentPerUser:
                    CheckMaxConcurrentPerUser(context, constraint, violations);
                    break;
                case ConstraintType.MaxActiveBookingsPerUser:
                    CheckMaxActiveBookings(context, constraint, violations);
                    break;
            }
        }

        return violations;
    }

    private static DateTimeOffset CurrentSlotStart(DateTimeOffset now) =>
        new(now.UtcTicks - (now.UtcTicks % SlotLength.Ticks), TimeSpan.Zero);

    private static void CheckCapacity(BookingContext context, List<RuleViolation> violations)
    {
        // Self-defending re-filter: only this resource's confirmed bookings that truly overlap
        // [Start, End). Back-to-back adjacency (end == next start) is allowed by the strict inequalities.
        var overlapping = context.OverlappingConfirmedBookings
            .Where(b => b.ResourceId == context.Resource.Id && b.Status == BookingStatus.Confirmed && Overlaps(b, context))
            .ToList();

        var capacity = context.Resource.Capacity;
        if (capacity == 1)
        {
            if (overlapping.Count > 0)
                violations.Add(new(RuleCodes.OverlapConflict, "This slot is already booked."));
            return;
        }

        if (context.Quantity > capacity)
        {
            violations.Add(new(RuleCodes.CapacityExceeded, $"This resource has {capacity} units in total."));
            return;
        }

        var used = PeakUsage(overlapping, b => b.Quantity, context.Start, context.End);
        if (used + context.Quantity > capacity)
        {
            violations.Add(new(RuleCodes.CapacityExceeded,
                $"Only {capacity - used} of {capacity} units are free in this window."));
        }
    }

    private static void CheckMaxDuration(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (ConstraintParams.Minutes(constraint.ParamsJson) is not { } minutes) return;
        if ((context.End - context.Start).TotalMinutes > minutes)
            violations.Add(new(RuleCodes.MaxDurationExceeded,
                $"Bookings on this resource are limited to {minutes} minutes."));
    }

    private static void CheckMaxAdvance(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (ConstraintParams.Days(constraint.ParamsJson) is not { } days) return;
        if (context.Start > context.Now.AddDays(days))
            violations.Add(new(RuleCodes.TooFarInAdvance,
                $"This resource can be booked at most {days} days ahead."));
    }

    private static void CheckMinNotice(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (ConstraintParams.Minutes(constraint.ParamsJson) is not { } minutes) return;
        if (context.Start < context.Now.AddMinutes(minutes))
            violations.Add(new(RuleCodes.InsufficientNotice,
                $"This resource needs at least {Describe(minutes)} notice."));
    }

    private static void CheckOpeningHours(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (ConstraintParams.OpeningHours(constraint.ParamsJson) is not { } days) return;

        var localStart = BusinessTime.ToLocal(context.Start);
        var localEnd = BusinessTime.ToLocal(context.End);

        // A booking must sit inside one window of its (local) starting day. Compare the
        // local DateTime, not the DateTimeOffset — the implicit conversion would re-read
        // the wall-clock in the server's zone and break in a UTC container.
        var endsAtMidnight = localEnd.DateTime == localStart.Date.AddDays(1);
        if (localStart.Date != localEnd.Date && !endsAtMidnight)
        {
            violations.Add(new(RuleCodes.OutsideOpeningHours, "Bookings cannot span multiple days."));
            return;
        }

        if (!days.TryGetValue(localStart.DayOfWeek, out var windows) || windows.Count == 0)
        {
            violations.Add(new(RuleCodes.OutsideOpeningHours, "This resource is closed on that day."));
            return;
        }

        var startTime = localStart.TimeOfDay;
        var endTime = endsAtMidnight ? TimeSpan.FromHours(24) : localEnd.TimeOfDay;
        if (!windows.Any(window => startTime >= window.Open && endTime <= window.Close))
        {
            var hours = string.Join(", ", windows.Select(window =>
                $"{ConstraintParams.Format(window.Open)}–{ConstraintParams.Format(window.Close)}"));
            violations.Add(new(RuleCodes.OutsideOpeningHours, $"Outside opening hours ({hours})."));
        }
    }

    private static void CheckMaxConcurrentPerUser(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (ConstraintParams.Count(constraint.ParamsJson) is not { } count) return;
        var own = context.OverlappingConfirmedBookings.Where(b =>
            b.ResourceId == context.Resource.Id &&
            b.Status == BookingStatus.Confirmed &&
            b.UserId == context.User.Id &&
            Overlaps(b, context));
        if (PeakUsage(own, _ => 1, context.Start, context.End) + 1 > count)
            violations.Add(new(RuleCodes.MaxConcurrentPerUser,
                $"You can hold at most {count} overlapping booking(s) on this resource."));
    }

    private static void CheckMaxActiveBookings(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (ConstraintParams.Count(constraint.ParamsJson) is not { } count) return;
        var upcoming = context.UsersUpcomingConfirmedBookings.Count(b =>
            b.ResourceId == context.Resource.Id &&
            b.Status == BookingStatus.Confirmed &&
            b.End > context.Now);
        if (upcoming + 1 > count)
            violations.Add(new(RuleCodes.MaxActiveBookings,
                $"You can hold at most {count} upcoming booking(s) on this resource."));
    }

    private static bool Overlaps(Booking booking, BookingContext context) =>
        booking.Start < context.End && booking.End > context.Start;

    /// <summary>
    /// The most units in use at any one instant of [start, end) — not the sum of every
    /// booking that touches the window, which would count 9–10 and 10–11 as simultaneous.
    /// Ends sort before starts at the same instant, so back-to-back bookings never stack.
    /// </summary>
    private static int PeakUsage(IEnumerable<Booking> bookings, Func<Booking, int> units, DateTimeOffset start, DateTimeOffset end)
    {
        var events = bookings
            .SelectMany(b => new[]
            {
                (At: b.Start > start ? b.Start : start, Delta: units(b)),
                (At: b.End < end ? b.End : end, Delta: -units(b)),
            })
            .OrderBy(e => e.At)
            .ThenBy(e => e.Delta);

        int current = 0, peak = 0;
        foreach (var (_, delta) in events)
        {
            current += delta;
            peak = Math.Max(peak, current);
        }
        return peak;
    }

    private static string Describe(int minutes)
    {
        if (minutes % 1440 == 0) return $"{minutes / 1440} day(s)";
        if (minutes % 60 == 0) return $"{minutes / 60} hour(s)";
        return $"{minutes} minute(s)";
    }
}
