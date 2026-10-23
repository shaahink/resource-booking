using Api.Contracts;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponse>> GetForUserAsync(Guid userId);
    Task MarkReadAsync(Guid userId, Guid notificationId);
}

public class NotificationService(AppDbContext db) : INotificationService
{
    private const int MaxReturned = 50;

    public async Task<IReadOnlyList<NotificationResponse>> GetForUserAsync(Guid userId) =>
        await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedOn)
            .Take(MaxReturned)
            .Select(n => new NotificationResponse(n.Id, n.Type, n.Message, n.BookingId, n.CreatedOn, n.ReadAt))
            .ToListAsync();

    public async Task MarkReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId)
            ?? throw new KeyNotFoundException("Notification not found.");
        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
}
