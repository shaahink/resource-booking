using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Contracts;
using Api.Data;
using Xunit;

namespace Api.Tests.Integration;

public class BookingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid AdminId = SeedData.AdminId;                                  // Priya (Admin)
    private static readonly Guid TomId = Guid.Parse("00000000-0000-0000-0000-000000000002"); // Tom (User)
    private static readonly Guid MayaId = Guid.Parse("00000000-0000-0000-0000-000000000003"); // Maya (User)

    private static readonly Guid WorkshopRoomId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid BoardroomId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>A weekday slot at the given London hour, `weeksAhead` weeks out — inside opening hours,
    /// far from the seeded fortnight. Each test uses its own week to stay independent.</summary>
    private static (DateTimeOffset Start, DateTimeOffset End) Slot(int weeksAhead, int hour, int durationMinutes = 60)
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var day = DateTime.UtcNow.Date;
        while (day.DayOfWeek != DayOfWeek.Tuesday) day = day.AddDays(1);
        day = day.AddDays(7 * weeksAhead);
        var local = new DateTime(day.Year, day.Month, day.Day, hour, 0, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, london.GetUtcOffset(local)).ToUniversalTime();
        return (start, start.AddMinutes(durationMinutes));
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/resources");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resources_are_seeded_and_listed()
    {
        var client = await factory.CreateClientForAsync(TomId);
        var resources = await client.GetFromJsonAsync<List<ResourceDto>>("/api/resources", Json);
        Assert.NotNull(resources);
        Assert.Equal(7, resources.Count);
        Assert.Contains(resources, r => r.Name == "Boardroom" && r.Constraints.Count == 2);
    }

    [Fact]
    public async Task Booking_happy_path_creates_and_shows_in_availability()
    {
        var client = await factory.CreateClientForAsync(TomId);
        var (start, end) = Slot(weeksAhead: 1, hour: 10);

        var response = await client.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, end, 1, "Team workshop"), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var availability = await client.GetFromJsonAsync<AvailabilityDto>(
            $"/api/resources/{WorkshopRoomId}/availability?from={Uri.EscapeDataString($"{start.AddHours(-1):O}")}&to={Uri.EscapeDataString($"{end.AddHours(1):O}")}", Json);
        var slot = Assert.Single(availability!.Bookings);
        Assert.True(slot.Mine);
    }

    [Fact]
    public async Task Overlapping_booking_returns_409_with_violation_codes()
    {
        var tom = await factory.CreateClientForAsync(TomId);
        var maya = await factory.CreateClientForAsync(MayaId);
        var (start, end) = Slot(weeksAhead: 2, hour: 10);

        var first = await tom.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, end, 1, null), Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await maya.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start.AddMinutes(30), end.AddMinutes(30), 1, null), Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("overlap_conflict", body);
    }

    [Fact]
    public async Task Rule_violation_returns_422()
    {
        var client = await factory.CreateClientForAsync(TomId);
        var (start, end) = Slot(weeksAhead: 3, hour: 22); // Workshop Room closes at 20:00

        var response = await client.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, end, 1, null), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("outside_opening_hours", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_bookings_for_one_slot_yield_exactly_one_success()
    {
        // The reason the advisory lock exists: two users submit the same slot at the same
        // instant. Without serialisation both check-then-insert paths would pass validation.
        var tom = await factory.CreateClientForAsync(TomId);
        var maya = await factory.CreateClientForAsync(MayaId);
        var (start, end) = Slot(weeksAhead: 4, hour: 14);

        var request = new CreateBookingRequest(BoardroomId, start, end, 1, null);
        var results = await Task.WhenAll(
            tom.PostAsJsonAsync("/api/bookings", request, Json),
            maya.PostAsJsonAsync("/api/bookings", request, Json));

        var statuses = results.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
    }

    [Fact]
    public async Task Admin_endpoints_enforce_the_admin_role()
    {
        var tom = await factory.CreateClientForAsync(TomId);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.GetAsync("/api/admin/users")).StatusCode);

        var admin = await factory.CreateClientForAsync(AdminId);
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/api/admin/users", Json);
        Assert.Equal(5, users!.Count);
    }

    [Fact]
    public async Task Admin_cancelling_someone_elses_booking_notifies_the_owner()
    {
        var maya = await factory.CreateClientForAsync(MayaId);
        var (start, end) = Slot(weeksAhead: 5, hour: 11);

        var created = await maya.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, end, 1, null), Json);
        var booking = await created.Content.ReadFromJsonAsync<BookingDto>(Json);

        var admin = await factory.CreateClientForAsync(AdminId);
        var cancel = await admin.DeleteAsync($"/api/bookings/{booking!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var notifications = await maya.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Json);
        Assert.Contains(notifications!, n => n.Type == "booking_cancelled" && n.BookingId == booking.Id);

        // And the cancelled slot no longer blocks the grid.
        var rebook = await maya.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, end, 1, null), Json);
        Assert.Equal(HttpStatusCode.Created, rebook.StatusCode);
    }

    [Fact]
    public async Task Users_cannot_cancel_other_peoples_bookings()
    {
        var maya = await factory.CreateClientForAsync(MayaId);
        var (start, end) = Slot(weeksAhead: 6, hour: 9);
        var created = await maya.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, end, 1, null), Json);
        var booking = await created.Content.ReadFromJsonAsync<BookingDto>(Json);

        var tom = await factory.CreateClientForAsync(TomId);
        var response = await tom.DeleteAsync($"/api/bookings/{booking!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_reports_utilisation_for_all_resources()
    {
        var admin = await factory.CreateClientForAsync(AdminId);
        var from = DateTimeOffset.UtcNow.AddDays(-14);
        var to = DateTimeOffset.UtcNow;
        var dashboard = await admin.GetFromJsonAsync<DashboardDto>(
            $"/api/admin/stats/dashboard?from={Uri.EscapeDataString($"{from:O}")}&to={Uri.EscapeDataString($"{to:O}")}", Json);

        Assert.Equal(7, dashboard!.Utilisation.Count);
        Assert.True(dashboard.BookingsThisWeek > 0, "seeded history should land inside the trailing week");
        Assert.Contains(dashboard.Utilisation, u => u.UtilisationPct > 0);
    }
}
