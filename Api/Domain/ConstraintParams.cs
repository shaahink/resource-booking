using System.Globalization;
using System.Text.Json;

namespace Api.Domain;

public sealed record OpeningWindow(TimeSpan Open, TimeSpan Close);

/// <summary>
/// The one reader for <see cref="ResourceConstraint.ParamsJson"/>. The validator rejects what it
/// cannot read on save, the rule engine skips it, and stats treat it as absent — all three ask
/// this class, so they can never disagree about what a valid constraint is.
/// Every reader returns null for anything malformed: a missing property, the wrong type, a
/// value out of range. A missing number never quietly becomes 0.
/// </summary>
public static class ConstraintParams
{
    public const int MaxMinutes = 525_600;
    public const int MaxDays = 3_650;
    public const int MaxCount = 1_000;

    private static readonly TimeSpan Midnight = TimeSpan.FromHours(24);

    private static readonly Dictionary<string, DayOfWeek> DayKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mon"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
        ["sun"] = DayOfWeek.Sunday,
    };

    public static bool IsValid(ConstraintType type, string paramsJson) => type switch
    {
        ConstraintType.MaxDurationMinutes or ConstraintType.MinNoticeMinutes => Minutes(paramsJson) is not null,
        ConstraintType.MaxAdvanceDays => Days(paramsJson) is not null,
        ConstraintType.MaxConcurrentPerUser or ConstraintType.MaxActiveBookingsPerUser => Count(paramsJson) is not null,
        ConstraintType.OpeningHours => OpeningHours(paramsJson) is not null,
        _ => false,
    };

    public static int? Minutes(string paramsJson) => PositiveInt(paramsJson, "minutes", MaxMinutes);

    public static int? Days(string paramsJson) => PositiveInt(paramsJson, "days", MaxDays);

    public static int? Count(string paramsJson) => PositiveInt(paramsJson, "count", MaxCount);

    /// <summary>
    /// <c>{"days":{"mon":[["08:00","18:00"]], …}}</c> in business-local time. Day keys are
    /// case-insensitive, times are strict <c>HH:mm</c>, <c>"24:00"</c> closes at midnight, and a
    /// day that is absent or has no windows is closed.
    /// </summary>
    public static IReadOnlyDictionary<DayOfWeek, IReadOnlyList<OpeningWindow>>? OpeningHours(string paramsJson)
    {
        using var doc = TryParse(paramsJson);
        if (doc is null || !doc.RootElement.TryGetProperty("days", out var days) || days.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<DayOfWeek, IReadOnlyList<OpeningWindow>>();
        foreach (var day in days.EnumerateObject())
        {
            if (!DayKeys.TryGetValue(day.Name, out var dayOfWeek) || day.Value.ValueKind != JsonValueKind.Array)
                return null;

            var windows = new List<OpeningWindow>();
            foreach (var window in day.Value.EnumerateArray())
            {
                if (window.ValueKind != JsonValueKind.Array || window.GetArrayLength() != 2
                    || !TryParseTime(window[0], out var open) || !TryParseTime(window[1], out var close)
                    || close <= open)
                    return null;

                windows.Add(new OpeningWindow(open, close));
            }
            result[dayOfWeek] = windows;
        }
        return result;
    }

    public static string Format(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}";

    private static int? PositiveInt(string paramsJson, string name, int max)
    {
        using var doc = TryParse(paramsJson);
        return doc is not null
               && doc.RootElement.TryGetProperty(name, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt32(out var number)
               && number >= 1 && number <= max
            ? number
            : null;
    }

    private static bool TryParseTime(JsonElement element, out TimeSpan time)
    {
        time = default;
        if (element.ValueKind != JsonValueKind.String) return false;

        var text = element.GetString();
        if (text == "24:00")
        {
            time = Midnight;
            return true;
        }
        return TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time);
    }

    private static JsonDocument? TryParse(string paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson)) return null;
        try
        {
            var doc = JsonDocument.Parse(paramsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc;
            doc.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
