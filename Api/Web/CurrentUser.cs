using System.Security.Claims;

namespace Api.Web;

public static class CurrentUser
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Not signed in."));

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole("Admin");
}
