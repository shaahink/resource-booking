using System.Security.Claims;
using Api.Contracts;
using Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Demo-mode auth, a deliberate product decision (see README): reviewers pick a seeded user and are
/// in within seconds — while roles are still enforced server-side on every endpoint, exactly as a
/// password flow would enforce them. Swapping the picker for real credentials/SSO touches only this file.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db) : ControllerBase
{
    /// <summary>The login screen's user picker.</summary>
    [HttpGet("users")]
    public async Task<IReadOnlyList<UserDto>> GetUsers() =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.Role))
            .ToListAsync();

    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginRequest request)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId);
        if (user is null) return Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

        return new UserDto(user.Id, user.Name, user.Email, user.Role);
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
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return user is null
            ? Unauthorized()
            : new UserDto(user.Id, user.Name, user.Email, user.Role);
    }
}
