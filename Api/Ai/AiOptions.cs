namespace Api.Ai;

/// <summary>Key comes from the OPENAI_API_KEY environment variable only — never from a file in the repo.</summary>
public record AiOptions(string? ApiKey, string Model)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

