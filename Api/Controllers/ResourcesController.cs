using Api.Contracts;
using Api.Domain;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/resources")]
[Authorize]
public class ResourcesController(IResourceService resources) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ResourceDto>> GetAll([FromQuery] ResourceType? type) =>
        resources.GetAllAsync(type, includeInactive: false);

    [HttpGet("{id:guid}")]
    public Task<ResourceDto> Get(Guid id) => resources.GetAsync(id);

    [HttpGet("{id:guid}/availability")]
    public Task<AvailabilityDto> GetAvailability(Guid id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to) =>
        resources.GetAvailabilityAsync(id, from, to, User.GetUserId());
}
