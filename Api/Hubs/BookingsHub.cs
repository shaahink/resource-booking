using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// Realtime channel. Clients join per-resource groups to receive bookingCreated / bookingCancelled
/// for the grid they are looking at; per-user notifications go to Clients.User(userId).
/// Server-side broadcasting lives in the booking service (task B2).
/// </summary>
[Authorize]
public class BookingsHub : Hub
{
    public Task WatchResource(Guid resourceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ResourceGroup(resourceId));

    public Task UnwatchResource(Guid resourceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ResourceGroup(resourceId));

    public static string ResourceGroup(Guid resourceId) => $"resource:{resourceId}";
}
