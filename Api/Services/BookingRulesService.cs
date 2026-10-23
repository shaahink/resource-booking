using System.Text.Json;
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
/// A malformed ParamsJson disables that one constraint rather than blocking all booking
/// of the resource — the admin UI validates shapes on entry, and a typo in one rule
/// should not take the resource offline.
/// </summary>
public sealed class BookingRulesService : IBookingRules
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

        // End, not Start: the half-hour that is currently running stays bookable,
        // matching the grid's definition of "past" (slot.end <= now).
        if (context.End <= context.Now)
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

    private static void CheckCapacity(BookingContext context, List<RuleViolation> violations)
    {
        // Self-defending re-filter: only confirmed bookings that truly overlap [Start, End).
        // Back-to-back adjacency (end == next start) is allowed by the strict inequalities.
        var overlapping = context.OverlappingConfirmedBookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.Start < context.End && b.End > context.Start)
            .ToList();

        if (context.Resource.Capacity == 1)
        {
            if (overlapping.Count > 0)
                violations.Add(new(RuleCodes.OverlapConflict, "This slot is already booked."));
            return;
        }

        var used = overlapping.Sum(b => b.Quantity);
        if (used + context.Quantity > context.Resource.Capacity)
        {
            var free = Math.Max(0, context.Resource.Capacity - used);
            violations.Add(new(RuleCodes.CapacityExceeded,
                $"Only {free} of {context.Resource.Capacity} units are free in this window."));
        }
    }

    private static void CheckMaxDuration(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (TryParse<MinutesParams>(constraint) is not { } p) return;
        if ((context.End - context.Start).TotalMinutes > p.Minutes)
            violations.Add(new(RuleCodes.MaxDurationExceeded,
                $"Bookings on this resource are limited to {p.Minutes} minutes."));
    }

    private static void CheckMaxAdvance(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (TryParse<DaysParams>(constraint) is not { } p) return;
        if (context.Start > context.Now.AddDays(p.Days))
            violations.Add(new(RuleCodes.TooFarInAdvance,
                $"This resource can be booked at most {p.Days} days ahead."));
    }

    private static void CheckMinNotice(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (TryParse<MinutesParams>(constraint) is not { } p) return;
        if (context.Start < context.Now.AddMinutes(p.Minutes))
            violations.Add(new(RuleCodes.InsufficientNotice,
                $"This resource needs at least {Describe(p.Minutes)} notice."));
    }

    private static void CheckOpeningHours(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (TryParse<OpeningHoursParams>(constraint) is not { Days: not null } p) return;

        var localStart = BusinessTime.ToLocal(context.Start);
        var localEnd = BusinessTime.ToLocal(context.End);

        // A booking must sit inside one window of its (local) starting day. Compare the
        // local DateTime, not the DateTimeOffset — the implicit conversion would re-read
        // the wall-clock in the server's zone and break in a UTC container.
        if (localStart.Date != localEnd.Date && localEnd.DateTime != localStart.Date.AddDays(1))
        {
            violations.Add(new(RuleCodes.OutsideOpeningHours, "Bookings cannot span multiple days."));
            return;
        }

        var dayKey = localStart.DayOfWeek switch
        {
            DayOfWeek.Monday => "mon",
            DayOfWeek.Tuesday => "tue",
            DayOfWeek.Wednesday => "wed",
            DayOfWeek.Thursday => "thu",
            DayOfWeek.Friday => "fri",
            DayOfWeek.Saturday => "sat",
            _ => "sun",
        };

        var windows = p.Days.TryGetValue(dayKey, out var w) ? w : null;
        if (windows is null || windows.Count == 0)
        {
            violations.Add(new(RuleCodes.OutsideOpeningHours, "This resource is closed on that day."));
            return;
        }

        var startTime = localStart.TimeOfDay;
        var endTime = localEnd.Date > localStart.Date ? TimeSpan.FromHours(24) : localEnd.TimeOfDay;
        var fits = windows.Any(window =>
            window.Count == 2 &&
            TimeSpan.TryParse(window[0], out var open) &&
            TimeSpan.TryParse(window[1], out var close) &&
            startTime >= open && endTime <= close);

        if (!fits)
        {
            var hours = string.Join(", ", windows.Where(w => w.Count == 2).Select(w => $"{w[0]}–{w[1]}"));
            violations.Add(new(RuleCodes.OutsideOpeningHours, $"Outside opening hours ({hours})."));
        }
    }

    private static void CheckMaxConcurrentPerUser(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (TryParse<CountParams>(constraint) is not { } p) return;
        var own = context.OverlappingConfirmedBookings.Count(b =>
            b.Status == BookingStatus.Confirmed &&
            b.UserId == context.User.Id &&
            b.Start < context.End && b.End > context.Start);
        if (own + 1 > p.Count)
            violations.Add(new(RuleCodes.MaxConcurrentPerUser,
                $"You can hold at most {p.Count} overlapping booking(s) on this resource."));
    }

    private static void CheckMaxActiveBookings(BookingContext context, ResourceConstraint constraint, List<RuleViolation> violations)
    {
        if (TryParse<CountParams>(constraint) is not { } p) return;
        var upcoming = context.UsersUpcomingConfirmedBookings.Count(b =>
            b.Status == BookingStatus.Confirmed && b.End > context.Now);
        if (upcoming + 1 > p.Count)
            violations.Add(new(RuleCodes.MaxActiveBookings,
                $"You can hold at most {p.Count} upcoming booking(s) on this resource."));
    }

    private static T? TryParse<T>(ResourceConstraint constraint) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(constraint.ParamsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Describe(int minutes) =>
        minutes % 1440 == 0 ? $"{minutes / 1440} day(s)"
        : minutes % 60 == 0 ? $"{minutes / 60} hour(s)"
        : $"{minutes} minute(s)";

    private sealed record MinutesParams(int Minutes);
    private sealed record DaysParams(int Days);
    private sealed record CountParams(int Count);
    private sealed record OpeningHoursParams(Dictionary<string, List<List<string>>>? Days);
}
