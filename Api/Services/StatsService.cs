using System.Text.Json;
using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IStatsService
{
    Task<DashboardDto> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to);
}

/// <summary>
/// Utilisation maths. Available hours per resource come from its OpeningHours constraint;
/// a resource without one is assumed bookable 08:00–18:00 on weekdays (documented
/// simplification — cars are physically bookable 24/7, but utilisation against a business
/// day is the number a facilities manager acts on). Pooled resources weight booked time
/// by quantity/capacity. Utilisation is clipped at 100%.
/// </summary>
public class StatsService(AppDbContext db) : IStatsService
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<DashboardDto> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var resources = await db.Resources.AsNoTracking().Include(r => r.Constraints).ToListAsync();
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed && b.Start < to && b.End > from)
            .ToListAsync();

        var utilisation = new List<ResourceUtilisationDto>();
        foreach (var resource in resources)
        {
            var own = bookings.Where(b => b.ResourceId == resource.Id).ToList();
            var bookedHours = own.Sum(b =>
            {
                var clippedStart = b.Start > from ? b.Start : from;
                var clippedEnd = b.End < to ? b.End : to;
                var hours = (clippedEnd - clippedStart).TotalHours;
                return resource.Capacity > 1 ? hours * b.Quantity / resource.Capacity : hours;
            });

            var availableHours = AvailableHours(resource, from, to);
            var pct = availableHours <= 0 ? 0 : Math.Min(100, bookedHours / availableHours * 100);

            utilisation.Add(new ResourceUtilisationDto(
                resource.Id, resource.Name, resource.Type,
                Math.Round(bookedHours, 1), Math.Round(availableHours, 1), Math.Round(pct), own.Count));
        }

        var peakHours = bookings
            .GroupBy(b => TimeZoneInfo.ConvertTime(b.Start, London).Hour)
            .Select(g => new PeakHourDto(g.Key, g.Count()))
            .OrderBy(p => p.Hour)
            .ToList();

        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name);
        var topUsers = bookings
            .GroupBy(b => b.UserId)
            .Select(g => new TopUserDto(g.Key, users.GetValueOrDefault(g.Key, "?"), g.Count()))
            .OrderByDescending(t => t.Count)
            .Take(5)
            .ToList();

        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var bookingsThisWeek = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.Status == BookingStatus.Confirmed && b.Start >= weekAgo);

        var totalBooked = utilisation.Sum(u => u.BookedHours);
        var totalAvailable = utilisation.Sum(u => u.AvailableHours);
        var overall = totalAvailable <= 0 ? 0 : Math.Min(100, Math.Round(totalBooked / totalAvailable * 100));

        return new DashboardDto(utilisation, peakHours, topUsers, bookingsThisWeek, overall);
    }

    private static double AvailableHours(Resource resource, DateTimeOffset from, DateTimeOffset to)
    {
        var windows = ParseOpeningHours(resource);
        var total = 0.0;

        var day = TimeZoneInfo.ConvertTime(from, London).Date;
        var lastDay = TimeZoneInfo.ConvertTime(to, London).Date;
        while (day <= lastDay)
        {
            total += windows.TryGetValue(day.DayOfWeek, out var dayWindows)
                ? dayWindows.Sum(w => (w.Close - w.Open).TotalHours)
                : 0;
            day = day.AddDays(1);
        }
        return total;
    }

    private static Dictionary<DayOfWeek, List<(TimeSpan Open, TimeSpan Close)>> ParseOpeningHours(Resource resource)
    {
        var constraint = resource.Constraints.FirstOrDefault(c => c.Type == ConstraintType.OpeningHours);
        if (constraint is null)
        {
            // Default business day for resources with no opening-hours constraint.
            var businessDay = new List<(TimeSpan, TimeSpan)> { (TimeSpan.FromHours(8), TimeSpan.FromHours(18)) };
            return new Dictionary<DayOfWeek, List<(TimeSpan, TimeSpan)>>
            {
                [DayOfWeek.Monday] = businessDay,
                [DayOfWeek.Tuesday] = businessDay,
                [DayOfWeek.Wednesday] = businessDay,
                [DayOfWeek.Thursday] = businessDay,
                [DayOfWeek.Friday] = businessDay,
            };
        }

        var result = new Dictionary<DayOfWeek, List<(TimeSpan, TimeSpan)>>();
        try
        {
            var parsed = JsonSerializer.Deserialize<OpeningHoursJson>(constraint.ParamsJson, JsonOptions);
            foreach (var (key, dayWindows) in parsed?.Days ?? [])
            {
                var dayOfWeek = key switch
                {
                    "mon" => DayOfWeek.Monday,
                    "tue" => DayOfWeek.Tuesday,
                    "wed" => DayOfWeek.Wednesday,
                    "thu" => DayOfWeek.Thursday,
                    "fri" => DayOfWeek.Friday,
                    "sat" => DayOfWeek.Saturday,
                    "sun" => DayOfWeek.Sunday,
                    _ => (DayOfWeek?)null,
                };
                if (dayOfWeek is null) continue;

                result[dayOfWeek.Value] = dayWindows
                    .Where(w => w.Count == 2)
                    .Select(w => (TimeSpan.Parse(w[0]), TimeSpan.Parse(w[1])))
                    .ToList();
            }
        }
        catch (Exception e) when (e is JsonException or FormatException)
        {
            // Malformed opening hours → no available time counted; the admin UI validates shapes.
        }
        return result;
    }

    private sealed record OpeningHoursJson(Dictionary<string, List<List<string>>>? Days);
}
