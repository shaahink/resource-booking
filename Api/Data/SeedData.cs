using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// Deterministic demo data: fixed ids for users and resources (stable login-as picker and links),
/// seeded Random for history. A realistic trailing fortnight plus a few upcoming bookings, so the
/// dashboard, history and availability grid are alive on first load.
/// </summary>
public static class SeedData
{
    public static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        // A plausible "this system has been running a while" timestamp for the seeded rows.
        var provisionedOn = DateTimeOffset.UtcNow.AddDays(-90);

        var admin = new User { Id = AdminId, Name = "Priya Sharma", Email = "priya@demo.local", Role = UserRole.Admin, CreatedOn = provisionedOn };
        var users = new List<User>
        {
            admin,
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Tom Hale", Email = "tom@demo.local", Role = UserRole.User, CreatedOn = provisionedOn },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Maya Chen", Email = "maya@demo.local", Role = UserRole.User, CreatedOn = provisionedOn },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Jonas Weber", Email = "jonas@demo.local", Role = UserRole.User, CreatedOn = provisionedOn },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Name = "Sofia Rossi", Email = "sofia@demo.local", Role = UserRole.User, CreatedOn = provisionedOn },
        };

        static ResourceConstraint C(ConstraintType type, string paramsJson) =>
            new() { Type = type, ParamsJson = paramsJson };

        const string weekdayEightToEight = """{"days":{"mon":[["08:00","20:00"]],"tue":[["08:00","20:00"]],"wed":[["08:00","20:00"]],"thu":[["08:00","20:00"]],"fri":[["08:00","20:00"]]}}""";
        const string lunchWindow = """{"days":{"mon":[["11:30","14:00"]],"tue":[["11:30","14:00"]],"wed":[["11:30","14:00"]],"thu":[["11:30","14:00"]],"fri":[["11:30","14:00"]]}}""";

        var resources = new List<Resource>
        {
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Name = "Boardroom", Type = ResourceType.Room, Capacity = 1, Seats = 14,
                Location = "1st floor", Description = "Large meeting room with video conferencing and whiteboard wall.",
                Constraints = [C(ConstraintType.OpeningHours, weekdayEightToEight), C(ConstraintType.MaxAdvanceDays, """{"days":60}""")],
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                Name = "Huddle A", Type = ResourceType.Room, Capacity = 1, Seats = 4,
                Location = "2nd floor", Description = "Small huddle room for quick syncs.",
                Constraints = [C(ConstraintType.OpeningHours, weekdayEightToEight), C(ConstraintType.MaxDurationMinutes, """{"minutes":120}""")],
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                Name = "Huddle B", Type = ResourceType.Room, Capacity = 1, Seats = 4,
                Location = "2nd floor", Description = "Small huddle room next to the kitchen.",
                Constraints = [C(ConstraintType.OpeningHours, weekdayEightToEight), C(ConstraintType.MaxDurationMinutes, """{"minutes":120}""")],
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                Name = "Workshop Room", Type = ResourceType.Room, Capacity = 1, Seats = 20,
                Location = "Ground floor", Description = "Training and workshop space with movable desks.",
                Constraints = [C(ConstraintType.OpeningHours, weekdayEightToEight)],
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
                Name = "Pool Car 1 (EV)", Type = ResourceType.Car, Capacity = 1,
                Location = "Car park B2", Description = "Electric hatchback, ~250 km range.",
                Constraints = [C(ConstraintType.MaxActiveBookingsPerUser, """{"count":2}"""), C(ConstraintType.MinNoticeMinutes, """{"minutes":60}""")],
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
                Name = "Pool Car 2 (Estate)", Type = ResourceType.Car, Capacity = 1,
                Location = "Car park B2", Description = "Estate car for equipment transport.",
                Constraints = [C(ConstraintType.MaxActiveBookingsPerUser, """{"count":2}"""), C(ConstraintType.MinNoticeMinutes, """{"minutes":60}""")],
            },
            new()
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
                Name = "Lunch Catering", Type = ResourceType.Catering, Capacity = 40,
                Location = "Kitchen", Description = "Catered lunch portions; book the number of meals you need.",
                Constraints = [C(ConstraintType.OpeningHours, lunchWindow), C(ConstraintType.MinNoticeMinutes, """{"minutes":1440}""")],
            },
        };

        foreach (var resource in resources) resource.CreatedOn = provisionedOn;

        db.Users.AddRange(users);
        db.Resources.AddRange(resources);

        // History: the trailing two working weeks, then a handful of upcoming bookings.
        var random = new Random(42);
        var bookings = new List<Booking>();
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, london).Date;
        int[] durations = [30, 60, 60, 90, 120];

        foreach (var dayOffset in Enumerable.Range(-13, 24)) // 13 days back to 10 days ahead — reviewers click into next week
        {
            var day = today.AddDays(dayOffset);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            foreach (var resource in resources)
            {
                if (resource.Type == ResourceType.Catering)
                {
                    if (random.NextDouble() < 0.7)
                        bookings.Add(MakeBooking(resource, users[random.Next(users.Count)], day, 12, 0, 90, random.Next(8, 30), london));
                    continue;
                }

                // Rooms busier than cars; walk forward through the day so slots never overlap.
                var slots = resource.Type == ResourceType.Room ? random.Next(2, 5) : random.Next(0, 2);
                var cursorHour = 8.0 + random.Next(0, 3);
                foreach (var _ in Enumerable.Range(0, slots))
                {
                    var duration = durations[random.Next(durations.Length)];
                    if (cursorHour + duration / 60.0 > 18) break;
                    bookings.Add(MakeBooking(resource, users[random.Next(users.Count)], day,
                        (int)cursorHour, (int)((cursorHour % 1) * 60), duration, 1, london));
                    cursorHour += duration / 60.0 + random.Next(0, 3) * 0.5;
                }
            }
        }

        // A couple of cancellations so the history view shows the status dimension.
        foreach (var booking in bookings.Where((_, i) => i % 11 == 0).Take(4))
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = booking.Start.AddHours(-random.Next(2, 48));
        }

        db.Bookings.AddRange(bookings);
        await db.SaveChangesAsync();
    }

    private static Booking MakeBooking(Resource resource, User user, DateTime day, int hour, int minute, int durationMinutes, int quantity, TimeZoneInfo zone)
    {
        var localStart = new DateTime(day.Year, day.Month, day.Day, hour, minute, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(localStart, zone.GetUtcOffset(localStart));
        return new Booking
        {
            ResourceId = resource.Id,
            UserId = user.Id,
            Start = start.ToUniversalTime(),
            End = start.AddMinutes(durationMinutes).ToUniversalTime(),
            Quantity = quantity,
            IsExclusive = resource.Capacity == 1,
            Status = BookingStatus.Confirmed,
            CreatedOn = start.AddDays(-2).ToUniversalTime(),
        };
    }
}
