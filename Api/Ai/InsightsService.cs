using System.Text.Json;
using Api.Contracts;
using Api.Services;
using OpenAI.Chat;

namespace Api.Ai;

public interface IInsightsService
{
    Task<InsightsSummaryDto> GetSummaryAsync();
}

/// <summary>
/// Plain-English utilisation digest for the dashboard. The numbers come from StatsService;
/// the model only narrates them — it is never asked to compute, and the card is labelled
/// as AI-generated in the UI.
/// </summary>
public class InsightsService(AiOptions options, IStatsService stats, ILogger<InsightsService> logger) : IInsightsService
{
    public async Task<InsightsSummaryDto> GetSummaryAsync()
    {
        if (!options.IsConfigured) throw new AiNotConfiguredException();

        var to = DateTimeOffset.UtcNow;
        var dashboard = await stats.GetDashboardAsync(to.AddDays(-14), to);
        var data = JsonSerializer.Serialize(new
        {
            days = 14,
            dashboard.OverallUtilisationPct,
            dashboard.BookingsThisWeek,
            utilisation = dashboard.Utilisation.Select(u => new { u.Name, u.Type, u.UtilisationPct, u.BookingsCount }),
            peakHours = dashboard.PeakHours.Where(p => p.Count > 0),
            topUsers = dashboard.TopUsers.Select(t => new { t.Name, t.Count }),
        });

        var client = new ChatClient(options.Model, options.ApiKey);
        try
        {
            ChatCompletion completion = await client.CompleteChatAsync(
                [
                    new SystemChatMessage(
                        "You write a 3-4 sentence utilisation digest for a facilities manager. " +
                        "Plain English, concrete, actionable: name the busiest and most underused " +
                        "resources and the peak window, and suggest one action. No bullet points, " +
                        "no numbers the data does not contain."),
                    new UserChatMessage(data),
                ],
                cancellationToken: new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

            return new InsightsSummaryDto(completion.Content[0].Text.Trim(), DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not AiNotConfiguredException)
        {
            logger.LogWarning(ex, "Insights generation failed");
            throw new AiNotConfiguredException();
        }
    }
}
