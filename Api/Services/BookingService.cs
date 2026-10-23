using Api.Contracts;
using Api.Data;
using Api.Domain;
using Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(Guid userId, CreateBookingRequest request);
    Task CancelAsync(Guid callerId, bool callerIsAdmin, Guid bookingId);
    Task<IReadOnlyList<BookingDto>> GetMineAsync(Guid userId);
    Task<IReadOnlyList<BookingDto>> GetHistoryAsync(Guid? resourceId, Guid? userId, DateTimeOffset? from, DateTimeOffset? to);
}

public class BookingService(
    AppDbContext db,
    IBookingRules rules,
    IHubContext<BookingsHub> hub,
    ILogger<BookingService> logger) : IBookingService
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public async Task<BookingDto> CreateAsync(Guid userId, CreateBookingRequest request)
    {
        var resource = await db.Resources.AsNoTracking()
            .Include(r => r.Constraints)
            .FirstOrDefaultAsync(r => r.Id == request.ResourceId)
            ?? throw new KeyNotFoundException("Resource not found.");
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);

        var booking = new Booking
        {
            ResourceId = resource.Id,
            UserId = userId,
            Start = request.Start.ToUniversalTime(),
            End = request.End.ToUniversalTime(),
            Quantity = request.Quantity,
            IsExclusive = resource.Capacity == 1,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // Serialise bookings per resource: the advisory lock makes check-then-insert atomic
            // against concurrent requests for the same resource, without table locks.
            await db.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({resource.Id.ToString()}, 0))");

            var overlapping = await db.Bookings.AsNoTracking()
                .Where(b => b.ResourceId == resource.Id
                            && b.Status == BookingStatus.Confirmed
                            && b.Start < booking.End && b.End > booking.Start)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var usersUpcoming = await db.Bookings.AsNoTracking()
                .Where(b => b.ResourceId == resource.Id
                            && b.UserId == userId
                            && b.Status == BookingStatus.Confirmed
                            && b.End > now)
                .ToListAsync();

            var violations = rules.Validate(new BookingContext(
                resource, resource.Constraints, user,
                booking.Start, booking.End, booking.Quantity,
                overlapping, usersUpcoming, now));
            if (violations.Count > 0) throw new BookingRejectedException(violations);

            db.Bookings.Add(booking);
            try
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
            {
                // The DB backstop (no_double_booking). With the advisory lock this should be
                // unreachable; it firing means a code path skipped the lock — log loudly.
                logger.LogError(ex, "Exclusion constraint caught a double booking for resource {ResourceId}", resource.Id);
                throw new BookingRejectedException([new(RuleCodes.OverlapConflict, "This slot is already booked.")]);
            }
        }

        logger.LogInformation("Booking {BookingId} created for {Resource} by {User}",
            booking.Id, resource.Name, user.Name);

        await hub.Clients.Group(BookingsHub.ResourceGroup(resource.Id))
            .SendAsync("bookingCreated", resource.Id, booking.Id);

        return ToDto(booking, resource.Name, resource.Type, user.Name);
    }

    public async Task CancelAsync(Guid callerId, bool callerIsAdmin, Guid bookingId)
    {
        var booking = await db.Bookings
            .Include(b => b.Resource)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.UserId != callerId && !callerIsAdmin)
            throw new UnauthorizedAccessException("Only the owner or an admin can cancel a booking.");
        if (booking.Status == BookingStatus.Cancelled) return;

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTimeOffset.UtcNow;

        string? message = null;
        if (booking.UserId != callerId)
        {
            var caller = await db.Users.AsNoTracking().FirstAsync(u => u.Id == callerId);
            message = $"Your {booking.Resource!.Name} booking on " +
                      $"{TimeZoneInfo.ConvertTime(booking.Start, London):ddd d MMM HH:mm} was cancelled by {caller.Name}.";
            db.Notifications.Add(new Notification
            {
                UserId = booking.UserId,
                BookingId = booking.Id,
                Type = "booking_cancelled",
                Message = message,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Booking {BookingId} cancelled by {CallerId}", booking.Id, callerId);

        await hub.Clients.Group(BookingsHub.ResourceGroup(booking.ResourceId))
            .SendAsync("bookingCancelled", booking.ResourceId, booking.Id);
        if (message is not null)
        {
            await hub.Clients.User(booking.UserId.ToString()).SendAsync("notification", message);
        }
    }

    public async Task<IReadOnlyList<BookingDto>> GetMineAsync(Guid userId) =>
        await db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.Start)
            .Select(b => new BookingDto(
                b.Id, b.ResourceId, b.Resource!.Name, b.Resource.Type,
                b.UserId, b.User!.Name, b.Start, b.End, b.Quantity, b.Status, b.Notes, b.CreatedAt))
            .ToListAsync();

    public async Task<IReadOnlyList<BookingDto>> GetHistoryAsync(
        Guid? resourceId, Guid? userId, DateTimeOffset? from, DateTimeOffset? to)
    {
        var query = db.Bookings.AsNoTracking();
        if (resourceId is not null) query = query.Where(b => b.ResourceId == resourceId);
        if (userId is not null) query = query.Where(b => b.UserId == userId);
        if (from is not null) query = query.Where(b => b.Start >= from);
        if (to is not null) query = query.Where(b => b.Start <= to);

        return await query
            .OrderByDescending(b => b.Start)
            .Take(500)
            .Select(b => new BookingDto(
                b.Id, b.ResourceId, b.Resource!.Name, b.Resource.Type,
                b.UserId, b.User!.Name, b.Start, b.End, b.Quantity, b.Status, b.Notes, b.CreatedAt))
            .ToListAsync();
    }

    private static BookingDto ToDto(Booking b, string resourceName, ResourceType type, string userName) =>
        new(b.Id, b.ResourceId, resourceName, type, b.UserId, userName,
            b.Start, b.End, b.Quantity, b.Status, b.Notes, b.CreatedAt);
}
