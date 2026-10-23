using System.Text.Json;
using Api.Contracts;
using Api.Domain;
using Api.Services;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;

namespace Api.Ai;

public interface IInsightsService
{
    Task<InsightsSummaryResponse> GetSummaryAsync();
}

/// <summary>
/// Plain-English utilisation digest for the dashboard. The numbers come from StatsService;
/// the model only narrates them — it is never asked to compute, and the card is labelled
/// as AI-generated in the UI. The payload carries no personal data: resource names and
/// counts only, never employee names. Each digest is a paid model call over data that
/// barely moves in minutes, so a successful one is cached briefly; a failure is not.
/// </summary>
public sealed class InsightsService(
    AiOptions options,
    IStatsService stats,
    IMemoryCache cache,
    ILogger<InsightsService> logger) : IInsightsService
{
    private const int DigestDays = 14;
    private const string CacheKey = "insights-digest";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ModelTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Structured output so the UI can format the card instead of rendering one text blob.
    private const string DigestSchema = """
        {
          "type": "object",
          "properties": {
            "headline": { "type": "string", "description": "One sentence: the overall picture." },
            "highlights": { "type": "array", "items": { "type": "string" }, "description": "2-4 short, concrete observations." },
            "suggestion": { "type": "string", "description": "One action the facilities manager should take." }
          },
          "required": ["headline", "highlights", "suggestion"],
          "additionalProperties": false
        }
        """;

    public async Task<InsightsSummaryResponse> GetSummaryAsync()
    {
        if (!options.IsConfigured) throw new AiNotConfiguredException();

        return (await cache.GetOrCreateAsync(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return GenerateAsync();
        }))!;
    }

    private async Task<InsightsSummaryResponse> GenerateAsync()
    {
        var to = DateTimeOffset.UtcNow;
        var dashboard = await stats.GetDashboardAsync(to.AddDays(-DigestDays), to);
        var data = JsonSerializer.Serialize(new
        {
            days = DigestDays,
            dashboard.OverallUtilisationPct,
            dashboard.BookingsInRange,
            utilisation = dashboard.Utilisation.Select(u => new { u.Name, u.Type, u.UtilisationPct, u.BookingsCount }),
            peakHours = dashboard.PeakHours.Where(p => p.Count > 0),
        });

        var client = new ChatClient(options.Model, options.ApiKey);
        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 400,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "utilisation_digest", BinaryData.FromString(DigestSchema), jsonSchemaIsStrict: true),
        };

        using var timeout = new CancellationTokenSource(ModelTimeout);
        try
        {
            ChatCompletion completion = await client.CompleteChatAsync(
                [
                    new SystemChatMessage(
                        "You write a utilisation digest for a facilities manager. " +
                        "Plain English, concrete, actionable: name the busiest and most underused " +
                        "resources and the peak window. No numbers the data does not contain."),
                    new UserChatMessage(data),
                ],
                completionOptions,
                timeout.Token);

            var digest = JsonSerializer.Deserialize<Digest>(completion.Content[0].Text, JsonOptions)
                ?? throw new JsonException("Empty digest");
            return new InsightsSummaryResponse(digest.Headline, digest.Highlights, digest.Suggestion, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not AiNotConfiguredException)
        {
            logger.LogWarning(ex, "Insights generation failed");
            throw new AiNotConfiguredException();
        }
    }

    private sealed record Digest(string Headline, IReadOnlyList<string> Highlights, string Suggestion);
}
