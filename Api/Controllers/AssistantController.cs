using Api.Ai;
using Api.Contracts;
using Api.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize]
public sealed class AssistantController(IAssistantService assistant) : ControllerBase
{
    [HttpPost("parse-booking")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public Task<AssistantParseResponse> ParseBooking(AssistantParseRequest request) =>
        assistant.ParseAsync(User.GetUserId(), request.Text);
}
