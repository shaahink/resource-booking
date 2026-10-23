using System.ClientModel;
using OpenAI;
using OpenAI.Chat;

namespace Api.Ai;

/// <summary>Key comes from the OPENAI_API_KEY environment variable only — never from a file in the repo.
/// <see cref="Endpoint"/> is null in production; tests point it somewhere unreachable.</summary>
public record AiOptions(string? ApiKey, string Model, Uri? Endpoint = null)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public ChatClient CreateChatClient() =>
        new(Model, new ApiKeyCredential(ApiKey ?? string.Empty),
            Endpoint is null ? new OpenAIClientOptions() : new OpenAIClientOptions { Endpoint = Endpoint });
}
