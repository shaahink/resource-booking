using System.Security.Claims;
using Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Api.Web;

/// <summary>
/// The auth cookie records who someone was at sign-in. Every request re-checks it against
/// the user row, so disabling or demoting a user takes effect on their next request — not
/// when the cookie expires — and a cookie for a user who no longer exists is a 401, not a 500.
/// </summary>
public static class SessionValidation
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var claimedRole = principal?.FindFirstValue(ClaimTypes.Role);
        var user = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? await context.HttpContext.RequestServices.GetRequiredService<AppDbContext>().Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.Role })
                .FirstOrDefaultAsync()
            : null;

        if (user is null || !user.IsActive || user.Role.ToString() != claimedRole)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
