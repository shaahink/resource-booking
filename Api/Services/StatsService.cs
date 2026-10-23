using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IStatsService
{
    Task<DashboardResponse> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to);
}

/// <summary>
/// Utilisation maths. Available hours per resource come from its OpeningHours constraint;
/// a resource without one is assumed bookable 08:00–18:00 on weekdays (documented
/// simplification — cars are physically bookable 24/7, but utilisation against a business
/// day is the number a facilities manager acts on). Pooled resources weight booked time
/// by quantity/capacity. Utilisation is clipped at 100%.
/// </summary>
public sealed class StatsService(AppDbContext db) : IStatsService
{
    // The peak-hours chart always spans the grid's visible day, so hours with no bookings
    // render as gaps instead of disappearing from the axis.
    private const int ChartFirstHour = 7;
    private const int ChartLastHour = 20;

    private const int TopUsersShown = 5;

    private static readonly TimeSpan DefaultOpen = TimeSpan.FromHours(8);
    private static readonly TimeSpan DefaultClose = TimeSpan.FromHours(18);

    public async Task<DashboardResponse> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to)
    {
        (from, to) = (from.ToUniversalTime(), to.ToUniversalTime());
        var resources = await db.Resources.AsNoTracking().Include(r => r.Constraints).ToListAsync();
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed && b.Start < to && b.End > from)
            .ToListAsync();
        var userNames = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name);

        var utilisation = resources
            .Select(r => Utilisation(r, bookings.Where(b => b.ResourceId == r.Id).ToList(), from, to))
            .ToList();

        return new DashboardResponse(
            utilisation,
            PeakHours(bookings),
            TopUsers(bookings, userNames),
            bookings.Count,
            OverallUtilisationPct(utilisation));
    }

    private static ResourceUtilisationResponse Utilisation(
        Resource resource, IReadOnlyList<Booking> own, DateTimeOffset from, DateTimeOffset to)
    {
        var bookedHours = own.Sum(b =>
        {
            var clippedStart = b.Start > from ? b.Start : from;
            var clippedEnd = b.End < to ? b.End : to;
            var hours = (clippedEnd - clippedStart).TotalHours;
            return resource.Capacity > 1 ? hours * b.Quantity / resource.Capacity : hours;
        });

        var availableHours = AvailableHours(resource, from, to);
        var pct = availableHours <= 0 ? 0 : Math.Min(100, bookedHours / availableHours * 100);

        return new ResourceUtilisationResponse(
            resource.Id, resource.Name, resource.Type,
            Math.Round(bookedHours, 1), Math.Round(availableHours, 1), Math.Round(pct), own.Count);
    }

    private static List<PeakHourResponse> PeakHours(IReadOnlyList<Booking> bookings)
    {
        var countsByHour = bookings
            .GroupBy(b => BusinessTime.ToLocal(b.Start).Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        var first = Math.Min(ChartFirstHour, countsByHour.Keys.DefaultIfEmpty(ChartFirstHour).Min());
        var last = Math.Max(ChartLastHour, countsByHour.Keys.DefaultIfEmpty(ChartLastHour).Max());

        return Enumerable.Range(first, last - first + 1)
            .Select(hour => new PeakHourResponse(hour, countsByHour.GetValueOrDefault(hour)))
            .ToList();
    }

    private static List<TopUserResponse> TopUsers(IReadOnlyList<Booking> bookings, Dictionary<Guid, string> userNames) =>
        bookings
            .GroupBy(b => b.UserId)
            .Select(g => new TopUserResponse(g.Key, userNames.GetValueOrDefault(g.Key, "?"), g.Count()))
            .OrderByDescending(t => t.Count)
            .Take(TopUsersShown)
            .ToList();

    private static double OverallUtilisationPct(IReadOnlyList<ResourceUtilisationResponse> utilisation)
    {
        var totalBooked = utilisation.Sum(u => u.BookedHours);
        var totalAvailable = utilisation.Sum(u => u.AvailableHours);
        return totalAvailable <= 0 ? 0 : Math.Min(100, Math.Round(totalBooked / totalAvailable * 100));
    }

    /// <summary>Opening-hours time inside [from, to]; windows on the boundary days are
    /// clipped to the range so short ranges don't over-count whole days.</summary>
    private static double AvailableHours(Resource resource, DateTimeOffset from, DateTimeOffset to)
    {
        var windows = OpeningWindows(resource);
        var localFrom = BusinessTime.ToLocal(from).DateTime;
        var localTo = BusinessTime.ToLocal(to).DateTime;

        var total = 0.0;
        for (var day = localFrom.Date; day <= localTo.Date; day = day.AddDays(1))
        {
            if (!windows.TryGetValue(day.DayOfWeek, out var dayWindows)) continue;

            foreach (var window in dayWindows)
            {
                var start = Max(day + window.Open, localFrom);
                var end = Min(day + window.Close, localTo);
                if (end > start) total += (end - start).TotalHours;
            }
        }
        return total;

        static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    }

    /// <summary>The resource's opening windows, or a weekday 08:00–18:00 business day when it has
    /// none — or has one the rule engine would skip as unreadable, so stats and rules agree.</summary>
    private static IReadOnlyDictionary<DayOfWeek, IReadOnlyList<OpeningWindow>> OpeningWindows(Resource resource)
    {
        var constraint = resource.Constraints.FirstOrDefault(c => c.Type == ConstraintType.OpeningHours);
        if (constraint is not null && ConstraintParams.OpeningHours(constraint.ParamsJson) is { } windows)
            return windows;

        IReadOnlyList<OpeningWindow> businessDay = [new(DefaultOpen, DefaultClose)];
        return new Dictionary<DayOfWeek, IReadOnlyList<OpeningWindow>>
        {
            [DayOfWeek.Monday] = businessDay,
            [DayOfWeek.Tuesday] = businessDay,
            [DayOfWeek.Wednesday] = businessDay,
            [DayOfWeek.Thursday] = businessDay,
            [DayOfWeek.Friday] = businessDay,
        };
    }
}
