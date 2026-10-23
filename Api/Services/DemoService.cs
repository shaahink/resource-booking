using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IDemoService
{
    Task ResetAsync();
}

/// <summary>
/// Wipes every table and reseeds the demo fortnight. The demo is public and editable by
/// design, so it needs an undo button; seeded ids are fixed, which is why the signed-in
/// admin's cookie keeps working across a reset.
/// </summary>
public sealed class DemoService(AppDbContext db, ILogger<DemoService> logger) : IDemoService
{
    public async Task ResetAsync()
    {
        // One transaction: a booking that lands mid-reset, or a failed seed, must not leave
        // a half-wiped database behind.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Notifications.ExecuteDeleteAsync();
        await db.Bookings.ExecuteDeleteAsync();
        await db.ResourceConstraints.ExecuteDeleteAsync();
        await db.Resources.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await SeedData.EnsureSeededAsync(db);
        await transaction.CommitAsync();

        logger.LogInformation("Demo data reset");
    }
}
