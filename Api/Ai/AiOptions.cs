using System.ClientModel;
using OpenAI;
using OpenAI.Chat;

namespace Api.Ai;

/// <summary>The "OpenAI" section of appsettings.json. The key comes from the OPENAI_API_KEY
/// environment variable only — never from a file in the repo.</summary>
public sealed record AiOptions
{
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "gpt-6-luna";

    /// <summary>Null in production; tests point it somewhere unreachable.</summary>
    public Uri? Endpoint { get; init; }

    /// <summary>How long one model call may take before the feature reports itself unavailable.</summary>
    public int TimeoutSeconds { get; init; } = 15;

    /// <summary>Days of history the insights digest covers.</summary>
    public int DigestDays { get; init; } = 14;

    /// <summary>How long a successful digest is reused; utilisation barely moves in minutes.</summary>
    public int DigestCacheMinutes { get; init; } = 10;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
    public TimeSpan DigestCache => TimeSpan.FromMinutes(DigestCacheMinutes);

    public ChatClient CreateChatClient() =>
        new(Model, new ApiKeyCredential(ApiKey ?? string.Empty),
            Endpoint is null ? new OpenAIClientOptions() : new OpenAIClientOptions { Endpoint = Endpoint });
}
