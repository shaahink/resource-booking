namespace Api.Domain;

/// <summary>
/// Business rules run on office wall-clock time; this is the one place that names the zone.
/// Storage stays UTC and the client renders the viewer's local time.
/// </summary>
public static class BusinessTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);
}
