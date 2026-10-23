using Api.Contracts;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId);
    Task MarkReadAsync(Guid userId, Guid notificationId);
}

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId) =>
        await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.BookingId, n.CreatedAt, n.ReadAt))
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
