using System.Globalization;
using Api.Domain;

namespace Api.Ai;

/// <summary>
/// What the model is allowed to tell us: intent, not actions. It never names resource ids
/// and never books — the planner resolves candidates and every proposal still passes
/// BookingRules before a user sees it.
/// </summary>
public record BookingIntent(
    string? ResourceType,
    int? SeatsNeeded,
    int? Quantity,
    string? Date,          // "yyyy-MM-dd", already resolved from relative words by the model
    string? StartTime,     // "HH:mm" local (Europe/London)
    int? DurationMinutes);

/// <summary>Pure intent → concrete window/quantity resolution, unit-tested without any AI in the loop.</summary>
public static class AssistantPlanner
{
    private static readonly TimeOnly MorningStart = new(9, 0);
    private const int EveningCutoffHour = 18;
    private const int DefaultDurationMinutes = 60;
    private const int MinDurationMinutes = 15;
    private const int MaxDurationMinutes = 8 * 60;
    private const int MaxDaysAhead = 365;

    /// <summary>Resolve the requested window in UTC. Defaults when no time is given: a future
    /// date means its morning; today means the next full hour (or tomorrow morning when the
    /// working day is over). Duration defaults to an hour, clamped to sane bounds.
    /// Null when the model returned a date or time that is not strict ISO, or a date in the
    /// past or more than a year out — model output is input, and gets validated like it.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End)? ResolveWindow(BookingIntent intent, DateTimeOffset now)
    {
        var localNow = BusinessTime.ToLocal(now);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        var date = today;
        if (intent.Date is not null
            && (!DateOnly.TryParseExact(intent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || date < today || date > today.AddDays(MaxDaysAhead)))
            return null;

        TimeOnly time;
        if (intent.StartTime is not null)
        {
            if (!TimeOnly.TryParseExact(intent.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                return null;
        }
        else if (date != today)
        {
            // "book a room on Friday" means Friday morning, not Friday at the current hour.
            time = MorningStart;
        }
        else
        {
            var nextFullHour = localNow.Hour + 1;
            if (nextFullHour >= EveningCutoffHour)
            {
                date = date.AddDays(1);
                time = MorningStart;
            }
            else
            {
                time = new TimeOnly(nextFullHour, 0);
            }
        }

        // "now" comes back from the model as the clock time (09:02); book the running slot instead.
        const int slotMinutes = BookingRules.SlotMinutes;
        var runningSlot = new TimeOnly(localNow.Hour, localNow.Minute / slotMinutes * slotMinutes);
        if (date == today && time >= runningSlot && time < runningSlot.AddMinutes(slotMinutes))
            time = runningSlot;

        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, BusinessTime.Zone.GetUtcOffset(local)).ToUniversalTime();
        var duration = Math.Clamp(intent.DurationMinutes ?? DefaultDurationMinutes, MinDurationMinutes, MaxDurationMinutes);
        return (start, start.AddMinutes(duration));
    }

    /// <summary>Quantity for the booking: pooled resources take the asked-for amount (meals ≈ people).</summary>
    public static int ResolveQuantity(BookingIntent intent, Resource resource) =>
        resource.Capacity == 1
            ? 1
            : Math.Clamp(intent.Quantity ?? intent.SeatsNeeded ?? 1, 1, resource.Capacity);

    /// <summary>Candidate resources for the intent, best fit first (smallest sufficient room).</summary>
    public static IReadOnlyList<Resource> RankCandidates(BookingIntent intent, IEnumerable<Resource> resources)
    {
        var type = RequestedType(intent);
        var candidates = resources.Where(r => r.IsActive && r.Type == type);

        if (type == ResourceType.Room && intent.SeatsNeeded is { } seats)
        {
            candidates = candidates.Where(r => r.Seats is null || r.Seats >= seats);
        }

        return candidates
            .OrderBy(r => r.Seats ?? int.MaxValue) // smallest room that fits first
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Said when nothing can be ranked at all. "Try another time" would send the user
    /// hunting for a slot that cannot exist, so name the real limit instead.</summary>
    public static string ExplainNoCandidates(BookingIntent intent, IEnumerable<Resource> resources)
    {
        var type = RequestedType(intent);
        var largest = resources
            .Where(r => r.IsActive && r.Type == type && r.Seats is not null)
            .MaxBy(r => r.Seats);

        if (type == ResourceType.Room && intent.SeatsNeeded is { } seats && largest is not null)
            return $"No room seats {seats} — the largest is {largest.Name} ({largest.Seats}).";

        var noun = type switch
        {
            ResourceType.Car => "pool car",
            ResourceType.Catering => "catering",
            _ => "room",
        };
        return $"There is no bookable {noun} right now.";
    }

    private static ResourceType RequestedType(BookingIntent intent) =>
        Enum.TryParse<ResourceType>(intent.ResourceType, ignoreCase: true, out var parsed)
            ? parsed
            : ResourceType.Room;
}
