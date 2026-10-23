using Api.Ai;
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
    IStatsService stats,
    IInsightsService insights,
    IDemoService demo) : ControllerBase
{
    // ── resources ──

    [HttpPost("resources")]
    public async Task<ActionResult<ResourceResponse>> CreateResource(SaveResourceRequest request)
    {
        var resource = await resources.CreateAsync(request);
        return Created($"/api/resources/{resource.Id}", resource);
    }

    [HttpPut("resources/{id:guid}")]
    public Task<ResourceResponse> UpdateResource(Guid id, SaveResourceRequest request) =>
        resources.UpdateAsync(id, request);

    [HttpGet("resources")]
    public Task<IReadOnlyList<ResourceResponse>> GetAllResources() =>
        resources.GetAllAsync(type: null, includeInactive: true);

    // ── users ──

    [HttpGet("users")]
    public Task<IReadOnlyList<UserResponse>> GetUsers() => users.GetAllAsync();

    [HttpPost("users")]
    public async Task<ActionResult<UserResponse>> CreateUser(SaveUserRequest request)
    {
        var user = await users.CreateAsync(request);
        return Created($"/api/admin/users/{user.Id}", user);
    }

    [HttpPut("users/{id:guid}")]
    public Task<UserResponse> UpdateUser(Guid id, SaveUserRequest request) => users.UpdateAsync(id, request);

    // ── history & stats ──

    [HttpGet("bookings")]
    public Task<IReadOnlyList<BookingResponse>> GetHistory(
        [FromQuery] Guid? resourceId, [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to) =>
        bookings.GetHistoryAsync(resourceId, userId, from, to);

    [HttpGet("stats/dashboard")]
    public Task<DashboardResponse> GetDashboard([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to) =>
        stats.GetDashboardAsync(from, to);

    [HttpGet("stats/summary")]
    public Task<InsightsSummaryResponse> GetSummary() => insights.GetSummaryAsync();

    // ── demo ──

    [HttpPost("demo/reset")]
    public async Task<IActionResult> ResetDemoData()
    {
        await demo.ResetAsync();
        return NoContent();
    }
}
