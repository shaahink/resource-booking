using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    IResourceService resources,
    IUserService users,
    IBookingService bookings,
    IStatsService stats) : ControllerBase
{
    // ── resources ──

    [HttpPost("resources")]
    public async Task<ActionResult<ResourceDto>> CreateResource(SaveResourceRequest request)
    {
        var resource = await resources.CreateAsync(request);
        return Created($"/api/resources/{resource.Id}", resource);
    }

    [HttpPut("resources/{id:guid}")]
    public Task<ResourceDto> UpdateResource(Guid id, SaveResourceRequest request) =>
        resources.UpdateAsync(id, request);

    [HttpGet("resources")]
    public Task<IReadOnlyList<ResourceDto>> GetAllResources() =>
        resources.GetAllAsync(type: null, includeInactive: true);

    // ── users ──

    [HttpGet("users")]
    public Task<IReadOnlyList<UserDto>> GetUsers() => users.GetAllAsync();

    [HttpPost("users")]
    public async Task<ActionResult<UserDto>> CreateUser(SaveUserRequest request)
    {
        var user = await users.CreateAsync(request);
        return Created($"/api/admin/users/{user.Id}", user);
    }

    [HttpPut("users/{id:guid}")]
    public Task<UserDto> UpdateUser(Guid id, SaveUserRequest request) => users.UpdateAsync(id, request);

    // ── history & stats ──

    [HttpGet("bookings")]
    public Task<IReadOnlyList<BookingDto>> GetHistory(
        [FromQuery] Guid? resourceId, [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to) =>
        bookings.GetHistoryAsync(resourceId, userId, from, to);

    [HttpGet("stats/dashboard")]
    public Task<DashboardDto> GetDashboard([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to) =>
        stats.GetDashboardAsync(from, to);
}
