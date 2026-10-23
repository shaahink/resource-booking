using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Contracts;
using Api.Data;
using Xunit;

namespace Api.Tests.Integration;

public sealed class BookingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid AdminId = SeedData.AdminId;                                  // Priya (Admin)
    private static readonly Guid TomId = Guid.Parse("00000000-0000-0000-0000-000000000002"); // Tom (User)
    private static readonly Guid MayaId = Guid.Parse("00000000-0000-0000-0000-000000000003"); // Maya (User)

    private static readonly Guid WorkshopRoomId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid BoardroomId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LunchCateringId = Guid.Parse("10000000-0000-0000-0000-000000000007"); // capacity 40

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>A Tuesday slot at the given London hour, inside opening hours and past the seeded
    /// bookings — the first Tuesday after them is week 1. Each test uses its own week to stay independent.</summary>
    private static (DateTimeOffset Start, DateTimeOffset End) Slot(int weeksAhead, int hour, int durationMinutes = 60)
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var day = DateTime.UtcNow.Date.AddDays(SeedData.DaysAhead + 1);
        while (day.DayOfWeek != DayOfWeek.Tuesday) day = day.AddDays(1);
        day = day.AddDays(7 * (weeksAhead - 1));
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
        var resources = await client.GetFromJsonAsync<List<ResourceResponse>>("/api/resources", Json);
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

        var availability = await client.GetFromJsonAsync<AvailabilityResponse>(
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
    public async Task Booking_in_the_past_returns_422()
    {
        var client = await factory.CreateClientForAsync(TomId);
        var start = DateTimeOffset.UtcNow.AddDays(-1);

        var response = await client.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, start.AddHours(1), 1, null), Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("in_the_past", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Duplicate_user_email_returns_409_with_a_message()
    {
        var admin = await factory.CreateClientForAsync(AdminId);
        var response = await admin.PostAsJsonAsync("/api/admin/users",
            new SaveUserRequest("Tom Clone", "tom@demo.local", Api.Domain.UserRole.User, true), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already exists", await response.Content.ReadAsStringAsync());
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
    public async Task Concurrent_pooled_bookings_never_exceed_capacity()
    {
        // Lunch Catering seats 40: five parallel requests for 10 each must yield exactly four
        // successes. Unlike the Boardroom case there is no exclusion constraint behind a pooled
        // resource, so this is the test that proves the advisory lock itself.
        var clients = await Task.WhenAll(
            new[] { TomId, MayaId, TomId, MayaId, AdminId }.Select(factory.CreateClientForAsync));
        var (start, end) = Slot(weeksAhead: 7, hour: 12);

        var request = new CreateBookingRequest(LunchCateringId, start, end, 10, null);
        var results = await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync("/api/bookings", request, Json)));

        Assert.Equal(4, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData("+01:00")]
    [InlineData("-05:00")]
    public async Task Availability_accepts_any_utc_offset(string offset)
    {
        var client = await factory.CreateClientForAsync(TomId);
        var response = await client.GetAsync(
            $"/api/resources/{WorkshopRoomId}/availability?from={Uri.EscapeDataString($"2026-11-03T08:00:00{offset}")}&to={Uri.EscapeDataString($"2026-11-10T08:00:00{offset}")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("")]                                                            // missing
    [InlineData("?from=2026-11-10T00:00:00Z&to=2026-11-03T00:00:00Z")]          // reversed
    [InlineData("?from=0001-01-01T00:00:00Z&to=9999-12-31T00:00:00Z")]          // unbounded
    public async Task Dashboard_rejects_a_missing_reversed_or_unbounded_range(string query)
    {
        var admin = await factory.CreateClientForAsync(AdminId);
        var response = await admin.GetAsync($"/api/admin/stats/dashboard{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""[null]""")]
    [InlineData("""[{"type":"MaxAdvanceDays","paramsJson":"{"days":3000000}"}]""")]
    [InlineData("""[{"type":"OpeningHours","paramsJson":"{"days":{"mon":"08:00-18:00"}}"}]""")]
    public async Task Resource_save_rejects_unusable_constraints(string constraintsJson)
    {
        var admin = await factory.CreateClientForAsync(AdminId);
        var body = $$"""{"name":"Probe","type":"Room","capacity":1,"isActive":true,"constraints":{{constraintsJson}}}""";
        var response = await admin.PostAsync("/api/admin/resources",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_disabled_users_existing_session_stops_working()
    {
        var sofiaId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var sofia = await factory.CreateClientForAsync(sofiaId);
        var admin = await factory.CreateClientForAsync(AdminId);
        var (start, end) = Slot(weeksAhead: 8, hour: 10);

        await admin.PutAsJsonAsync($"/api/admin/users/{sofiaId}",
            new SaveUserRequest("Sofia Rossi", "sofia@demo.local", Api.Domain.UserRole.User, IsActive: false), Json);
        try
        {
            var response = await sofia.PostAsJsonAsync("/api/bookings",
                new CreateBookingRequest(WorkshopRoomId, start, end, 1, null), Json);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/admin/users/{sofiaId}",
                new SaveUserRequest("Sofia Rossi", "sofia@demo.local", Api.Domain.UserRole.User, IsActive: true), Json);
        }
    }

    [Fact]
    public async Task A_demoted_admin_loses_the_admin_endpoints_immediately()
    {
        var jonasId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var admin = await factory.CreateClientForAsync(AdminId);
        await admin.PutAsJsonAsync($"/api/admin/users/{jonasId}",
            new SaveUserRequest("Jonas Weber", "jonas@demo.local", Api.Domain.UserRole.Admin, IsActive: true), Json);
        var jonas = await factory.CreateClientForAsync(jonasId);
        Assert.Equal(HttpStatusCode.OK, (await jonas.GetAsync("/api/admin/users")).StatusCode);

        await admin.PutAsJsonAsync($"/api/admin/users/{jonasId}",
            new SaveUserRequest("Jonas Weber", "jonas@demo.local", Api.Domain.UserRole.User, IsActive: true), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, (await jonas.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task The_login_picker_does_not_expose_emails()
    {
        var anonymous = factory.CreateClient();
        var body = await anonymous.GetStringAsync("/api/auth/users");
        Assert.Contains("Priya Sharma", body);
        Assert.DoesNotContain("@", body);
    }

    [Fact]
    public async Task Unknown_api_routes_are_404_not_the_spa()
    {
        var tom = await factory.CreateClientForAsync(TomId);
        var response = await tom.GetAsync("/api/bokings");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Admin_endpoints_enforce_the_admin_role()
    {
        var tom = await factory.CreateClientForAsync(TomId);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.GetAsync("/api/admin/users")).StatusCode);

        var admin = await factory.CreateClientForAsync(AdminId);
        var users = await admin.GetFromJsonAsync<List<UserResponse>>("/api/admin/users", Json);
        Assert.Equal(5, users!.Count);
    }

    [Fact]
    public async Task Admin_cancelling_someone_elses_booking_notifies_the_owner()
    {
        var maya = await factory.CreateClientForAsync(MayaId);
        var (start, end) = Slot(weeksAhead: 5, hour: 11);

        var created = await maya.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(WorkshopRoomId, start, end, 1, null), Json);
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>(Json);

        var admin = await factory.CreateClientForAsync(AdminId);
        var cancel = await admin.DeleteAsync($"/api/bookings/{booking!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var notifications = await maya.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications", Json);
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
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>(Json);

        var tom = await factory.CreateClientForAsync(TomId);
        var response = await tom.DeleteAsync($"/api/bookings/{booking!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ai_endpoints_return_503_when_no_key_is_configured()
    {
        // CI and the test factory run keyless by design: the AI features must degrade, not break.
        var tom = await factory.CreateClientForAsync(TomId);
        var parse = await tom.PostAsJsonAsync("/api/assistant/parse-booking", new { text = "book a room" }, Json);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, parse.StatusCode);

        var admin = await factory.CreateClientForAsync(AdminId);
        var summary = await admin.GetAsync("/api/admin/stats/summary");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, summary.StatusCode);
    }

    [Fact]
    public async Task Dashboard_reports_utilisation_for_all_resources()
    {
        var admin = await factory.CreateClientForAsync(AdminId);
        var from = DateTimeOffset.UtcNow.AddDays(-14);
        var to = DateTimeOffset.UtcNow;
        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>(
            $"/api/admin/stats/dashboard?from={Uri.EscapeDataString($"{from:O}")}&to={Uri.EscapeDataString($"{to:O}")}", Json);

        Assert.Equal(7, dashboard!.Utilisation.Count);
        Assert.True(dashboard.BookingsInRange > 0, "seeded history should land inside the trailing fortnight");
        Assert.Contains(dashboard.Utilisation, u => u.UtilisationPct > 0);
    }
}
