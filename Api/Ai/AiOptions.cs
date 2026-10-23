namespace Api.Ai;

/// <summary>Key comes from the OPENAI_API_KEY environment variable only — never from a file in the repo.</summary>
public record AiOptions(string? ApiKey, string Model)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>Mapped to 503 by ApiExceptionHandler: the feature is absent, not broken.</summary>
public class AiNotConfiguredException() : Exception("AI features are not configured on this deployment.");
