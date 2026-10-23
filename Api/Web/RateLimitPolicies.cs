using System.Threading.RateLimiting;

namespace Api.Web;

public static class RateLimitPolicies
{
    /// <summary>Paid model calls. The demo lets anyone sign in as anyone, so the limit is per
    /// client address, not per user — picking another user from the list does not reset it.</summary>
    public const string Ai = "ai";

    public static RateLimitPartition<string> AiPartition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) });
}
