using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NotificationResponse>> GetMine() =>
        notifications.GetForUserAsync(User.GetUserId());

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await notifications.MarkReadAsync(User.GetUserId(), id);
        return NoContent();
    }
}
