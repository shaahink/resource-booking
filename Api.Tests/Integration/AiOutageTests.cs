using System.Net;
using System.Net.Http.Json;
using Api.Ai;
using Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>A configured key whose provider is down must read as an outage (502), never as
/// "not configured" (503) and never as the user's wording being unreadable.</summary>
public sealed class AiOutageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_failing_model_provider_is_a_502_on_both_ai_endpoints()
    {
        using var outage = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<AiOptions>();
            services.AddSingleton(new AiOptions("sk-test", "gpt-4.1-nano", new Uri("http://127.0.0.1:9/v1")));
        }));
        var client = outage.CreateClient(new() { HandleCookies = true });
        await client.PostAsJsonAsync("/api/auth/login", new { userId = SeedData.AdminId });

        var parse = await client.PostAsJsonAsync("/api/assistant/parse-booking", new { text = "book a room" });
        Assert.Equal(HttpStatusCode.BadGateway, parse.StatusCode);

        var summary = await client.GetAsync("/api/admin/stats/summary");
        Assert.Equal(HttpStatusCode.BadGateway, summary.StatusCode);
    }
}
