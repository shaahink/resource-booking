using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>Its own factory: the limiter's window is shared state, and the other AI test
/// must not inherit an exhausted one.</summary>
public sealed class RateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ai_endpoints_are_rate_limited_per_client()
    {
        var client = await factory.CreateClientForAsync(SeedData.AdminId);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            var response = await client.PostAsJsonAsync("/api/assistant/parse-booking", new { text = "book a room" });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(10), s => Assert.Equal(HttpStatusCode.ServiceUnavailable, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
    }
}
