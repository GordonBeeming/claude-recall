using System.Text.Json.Serialization;

namespace ClaudeRecall.Models;

public sealed class AiSearchTermsResponse
{
    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = [];
}

public sealed class AiValidationResponse
{
    [JsonPropertyName("results")]
    public List<AiSessionCandidate> Results { get; set; } = [];
}

public sealed class AiSessionCandidate
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("matches")]
    public bool Matches { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

public sealed class AiSummaryResponse
{
    [JsonPropertyName("summaries")]
    public Dictionary<string, string> Summaries { get; set; } = new();
}
