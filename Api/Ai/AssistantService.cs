using System.Text.Json;
using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace Api.Ai;

public interface IAssistantService
{
    Task<AssistantParseResponse> ParseAsync(Guid userId, string text);
}

/// <summary>
/// The NL booking assistant. gpt-4.1-nano does exactly one job — turn free text into a
/// BookingIntent via a strict JSON schema. Everything after that is deterministic server
/// code: candidate ranking, window resolution, and a BookingRules dry-run per candidate.
/// A proposal the user sees has already passed the same validation a form booking would.
/// </summary>
public class AssistantService(
    AiOptions options,
    AppDbContext db,
    IBookingRules rules,
    ILogger<AssistantService> logger) : IAssistantService
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string IntentSchema = """
        {
          "type": "object",
          "properties": {
            "resourceType": { "type": ["string", "null"], "enum": ["Room", "Car", "Catering", null] },
            "seatsNeeded": { "type": ["integer", "null"] },
            "quantity": { "type": ["integer", "null"] },
            "date": { "type": ["string", "null"], "description": "yyyy-MM-dd, resolved from relative words" },
            "startTime": { "type": ["string", "null"], "description": "HH:mm, 24h, Europe/London" },
            "durationMinutes": { "type": ["integer", "null"] }
          },
          "required": ["resourceType", "seatsNeeded", "quantity", "date", "startTime", "durationMinutes"],
          "additionalProperties": false
        }
        """;

    public async Task<AssistantParseResponse> ParseAsync(Guid userId, string text)
    {
        if (!options.IsConfigured) throw new AiNotConfiguredException();

        var intent = await ExtractIntentAsync(text);
        if (intent is null)
        {
            return new AssistantParseResponse(
                "I couldn't read a booking request out of that — try something like “book a room for 6 tomorrow at 14:00”.",
                []);
        }

        var resources = await db.Resources.AsNoTracking()
            .Include(r => r.Constraints)
            .Where(r => r.IsActive)
            .ToListAsync();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);

        var now = DateTimeOffset.UtcNow;
        var (start, end) = AssistantPlanner.ResolveWindow(intent, now);
        var candidates = AssistantPlanner.RankCandidates(intent, resources);

        var proposals = new List<AssistantProposalDto>();
        IReadOnlyList<RuleViolation> firstRejection = [];
        foreach (var resource in candidates)
        {
            if (proposals.Count == 3) break;

            var quantity = AssistantPlanner.ResolveQuantity(intent, resource);
            var overlapping = await db.Bookings.AsNoTracking()
                .Where(b => b.ResourceId == resource.Id
                            && b.Status == BookingStatus.Confirmed
                            && b.Start < end && b.End > start)
                .ToListAsync();
            var usersUpcoming = await db.Bookings.AsNoTracking()
                .Where(b => b.ResourceId == resource.Id
                            && b.UserId == userId
                            && b.Status == BookingStatus.Confirmed
                            && b.End > now)
                .ToListAsync();

            var violations = rules.Validate(new BookingContext(
                resource, resource.Constraints, user, start, end, quantity,
                overlapping, usersUpcoming, now));

            if (violations.Count == 0)
            {
                proposals.Add(new AssistantProposalDto(resource.Id, resource.Name, start, end, quantity));
            }
            else if (firstRejection.Count == 0)
            {
                firstRejection = violations;
            }
        }

        var localStart = TimeZoneInfo.ConvertTime(start, London);
        var reason = firstRejection.Count > 0 ? $" {firstRejection[0].Message}" : string.Empty;
        var summary = proposals.Count > 0
            ? $"Free for {localStart:ddd d MMM HH:mm}–{TimeZoneInfo.ConvertTime(end, London):HH:mm} — pick one to book:"
            : $"Nothing free matched {localStart:ddd d MMM HH:mm}.{reason} Try another time, or the availability grid.";
        return new AssistantParseResponse(summary, proposals);
    }

    /// <summary>Small models are unreliable at weekday arithmetic — hand them the answer sheet.</summary>
    private static string BuildCalendar(DateTimeOffset localNow) =>
        string.Join(", ", Enumerable.Range(0, 14).Select(i => localNow.AddDays(i).ToString("ddd yyyy-MM-dd")));

    private async Task<BookingIntent?> ExtractIntentAsync(string text)
    {
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, London);
        var client = new ChatClient(options.Model, options.ApiKey);

        var completionOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "booking_intent", BinaryData.FromString(IntentSchema), jsonSchemaIsStrict: true),
        };

        try
        {
            ChatCompletion completion = await client.CompleteChatAsync(
                [
                    new SystemChatMessage(
                        $"You extract booking intent for an office resource booking system " +
                        $"(meeting rooms, pool cars, lunch catering). " +
                        $"Now is {localNow:dddd yyyy-MM-dd HH:mm} in Europe/London. " +
                        $"Resolve relative dates like 'tomorrow' or 'next Tuesday' to yyyy-MM-dd " +
                        $"using this calendar of the next two weeks: {BuildCalendar(localNow)}. " +
                        $"Return null for anything the user did not say."),
                    new UserChatMessage(text),
                ],
                completionOptions,
                new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

            return JsonSerializer.Deserialize<BookingIntent>(completion.Content[0].Text, JsonOptions);
        }
        catch (Exception ex) when (ex is not AiNotConfiguredException)
        {
            // Model output is an enhancement, never a dependency: fail soft and let the
            // user fall back to the grid.
            logger.LogWarning(ex, "Assistant intent extraction failed");
            return null;
        }
    }
}
