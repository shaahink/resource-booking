using Api.Contracts;
using Api.Domain;
using Api.Services;
using Api.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/resources")]
[Authorize]
public sealed class ResourcesController(IResourceService resources) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ResourceResponse>> GetAll([FromQuery] ResourceType? type) =>
        resources.GetAllAsync(type, includeInactive: false);

    [HttpGet("{id:guid}")]
    public Task<ResourceResponse> Get(Guid id) => resources.GetAsync(id);

    [HttpGet("{id:guid}/availability")]
    public Task<AvailabilityResponse> GetAvailability(Guid id, [FromQuery] DateRangeQuery range) =>
        resources.GetAvailabilityAsync(id, range.From, range.To, User.GetUserId());
}
