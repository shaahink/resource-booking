using System.Security.Claims;
using Api.Contracts;
using Api.Services;
using Api.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Demo-mode auth, a deliberate product decision (see README): reviewers pick a seeded user and are
/// in within seconds — while roles are still enforced server-side on every endpoint, exactly as a
/// password flow would enforce them. Swapping the picker for real credentials/SSO touches only this file.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IUserService users) : ControllerBase
{
    [HttpGet("users")]
    public Task<IReadOnlyList<LoginOptionResponse>> GetUsers() => users.GetLoginOptionsAsync();

    [HttpPost("login")]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request)
    {
        var user = await users.FindActiveAsync(request.UserId);
        if (user is null) return Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

        return user;
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me() =>
        await users.FindActiveAsync(User.GetUserId()) is { } user ? user : Unauthorized();
}
