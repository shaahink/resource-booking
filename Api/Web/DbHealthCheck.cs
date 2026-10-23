using Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Web;

/// <summary>Makes /health mean something: the app is only healthy if the database answers.</summary>
public sealed class DbHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot reach the database.");
}
