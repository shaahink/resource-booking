using System.Globalization;
using Api.Ai;
using Api.Domain;
using Xunit;

namespace Api.Tests.Unit;

public sealed class AssistantPlannerTests
{
    // Monday 19 Oct 2026, 09:00 Europe/London (BST).
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T08:00:00Z", CultureInfo.InvariantCulture);

    private static BookingIntent Intent(
        string? type = null, int? seats = null, int? quantity = null,
        string? date = null, string? startTime = null, int? duration = null) =>
        new(type, seats, quantity, date, startTime, duration);

    private static Resource Resource(ResourceType type, int capacity = 1, int? seats = null, string name = "R", bool active = true) =>
        new() { Id = Guid.NewGuid(), Name = name, Type = type, Capacity = capacity, Seats = seats, IsActive = active };

    private static (DateTimeOffset Start, DateTimeOffset End) Window(BookingIntent intent, DateTimeOffset now)
    {
        var window = AssistantPlanner.ResolveWindow(intent, now);
        Assert.NotNull(window);
        return window.Value;
    }

    [Fact]
    public void Explicit_date_and_time_resolve_to_london_local()
    {
        var (start, end) = Window(
            Intent(date: "2026-10-20", startTime: "14:00", duration: 90), Now);

        Assert.Equal(DateTimeOffset.Parse("2026-10-20T13:00:00Z", CultureInfo.InvariantCulture), start); // 14:00 BST == 13:00 UTC
        Assert.Equal(90, (end - start).TotalMinutes);
    }

    [Fact]
    public void Missing_time_defaults_to_the_next_full_hour_today()
    {
        var (start, _) = Window(Intent(), Now);
        Assert.Equal(DateTimeOffset.Parse("2026-10-19T09:00:00Z", CultureInfo.InvariantCulture), start); // 10:00 London
    }

    [Fact]
    public void Late_in_the_day_rolls_to_tomorrow_morning()
    {
        var evening = DateTimeOffset.Parse("2026-10-19T18:30:00Z", CultureInfo.InvariantCulture); // 19:30 London
        var (start, _) = Window(Intent(), evening);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T08:00:00Z", CultureInfo.InvariantCulture), start); // tomorrow 09:00 London
    }

    [Fact]
    public void Missing_time_on_a_future_date_defaults_to_morning()
    {
        // "book a room on Wednesday", asked Monday evening: Wednesday 09:00, not 20:00.
        var evening = DateTimeOffset.Parse("2026-10-19T18:30:00Z", CultureInfo.InvariantCulture);
        var (start, _) = Window(Intent(date: "2026-10-21"), evening);
        Assert.Equal(DateTimeOffset.Parse("2026-10-21T08:00:00Z", CultureInfo.InvariantCulture), start); // 09:00 London
    }

    [Fact]
    public void Duration_is_clamped_to_sane_bounds()
    {
        var (start, end) = Window(Intent(duration: 10_000), Now);
        Assert.Equal(8 * 60, (end - start).TotalMinutes);
    }

    [Theory]
    [InlineData("9999-12-31", null)]   // past the one-year horizon (would overflow AddMinutes)
    [InlineData("2026-10-18", null)]   // yesterday
    [InlineData("20/10/2026", null)]   // not ISO
    [InlineData("2026-10-20", "2pm")]  // time not HH:mm
    public void Unusable_model_dates_and_times_resolve_to_no_window(string date, string? startTime)
    {
        Assert.Null(AssistantPlanner.ResolveWindow(Intent(date: date, startTime: startTime), Now));
    }

    [Fact]
    public void Exclusive_resources_always_book_quantity_one()
    {
        var room = Resource(ResourceType.Room, seats: 10);
        Assert.Equal(1, AssistantPlanner.ResolveQuantity(Intent(seats: 6, quantity: 6), room));
    }

    [Fact]
    public void Pooled_quantity_falls_back_to_seats_and_clamps_to_capacity()
    {
        var catering = Resource(ResourceType.Catering, capacity: 40);
        Assert.Equal(12, AssistantPlanner.ResolveQuantity(Intent(seats: 12), catering));
        Assert.Equal(40, AssistantPlanner.ResolveQuantity(Intent(quantity: 99), catering));
    }

    [Fact]
    public void Candidates_prefer_the_smallest_sufficient_room()
    {
        var resources = new[]
        {
            Resource(ResourceType.Room, seats: 20, name: "Workshop"),
            Resource(ResourceType.Room, seats: 4, name: "Huddle"),
            Resource(ResourceType.Room, seats: 14, name: "Boardroom"),
            Resource(ResourceType.Car, name: "Car"),
        };

        var ranked = AssistantPlanner.RankCandidates(Intent(type: "Room", seats: 6), resources);
        Assert.Equal(["Boardroom", "Workshop"], ranked.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void Inactive_resources_and_other_types_are_excluded()
    {
        var resources = new[]
        {
            Resource(ResourceType.Car, name: "Car A"),
            Resource(ResourceType.Car, name: "Car B", active: false),
            Resource(ResourceType.Room, name: "Room"),
        };

        var ranked = AssistantPlanner.RankCandidates(Intent(type: "Car"), resources);
        Assert.Equal(["Car A"], ranked.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void Unknown_type_defaults_to_rooms()
    {
        var resources = new[] { Resource(ResourceType.Room, name: "Room"), Resource(ResourceType.Car, name: "Car") };
        var ranked = AssistantPlanner.RankCandidates(Intent(type: "spaceship"), resources);
        Assert.Equal(["Room"], ranked.Select(r => r.Name).ToArray());
    }
}
