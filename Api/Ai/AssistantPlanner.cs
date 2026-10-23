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
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    /// <summary>Resolve the requested window in UTC. Defaults: next full hour today (or tomorrow
    /// 09:00 when the day is over), 60 minutes.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) ResolveWindow(BookingIntent intent, DateTimeOffset now)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, London);

        var date = DateOnly.TryParse(intent.Date, out var parsedDate)
            ? parsedDate
            : DateOnly.FromDateTime(localNow.DateTime);

        TimeOnly time;
        if (TimeOnly.TryParse(intent.StartTime, out var parsedTime))
        {
            time = parsedTime;
        }
        else
        {
            var nextFullHour = localNow.Hour + 1;
            if (nextFullHour >= 18 && intent.Date is null)
            {
                date = date.AddDays(1);
                time = new TimeOnly(9, 0);
            }
            else
            {
                time = new TimeOnly(Math.Min(nextFullHour, 23), 0);
            }
        }

        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, London.GetUtcOffset(local)).ToUniversalTime();
        var duration = Math.Clamp(intent.DurationMinutes ?? 60, 15, 8 * 60);
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
        var type = Enum.TryParse<ResourceType>(intent.ResourceType, ignoreCase: true, out var parsed)
            ? parsed
            : ResourceType.Room;

        var candidates = resources.Where(r => r.IsActive && r.Type == type);

        if (type == ResourceType.Room && intent.SeatsNeeded is { } seats)
        {
            candidates = candidates.Where(r => r.Seats is null || r.Seats >= seats);
        }

        return candidates
            .OrderBy(r => r.Seats ?? int.MaxValue) // smallest room that fits first
            .ThenBy(r => r.Name)
            .ToList();
    }
}
