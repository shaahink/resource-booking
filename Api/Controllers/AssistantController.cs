using Api.Ai;
using Api.Contracts;
using Api.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize]
public sealed class AssistantController(IAssistantService assistant) : ControllerBase
{
    [HttpPost("parse-booking")]
    public Task<AssistantParseResponse> ParseBooking(AssistantParseRequest request) =>
        assistant.ParseAsync(User.GetUserId(), request.Text);
}
