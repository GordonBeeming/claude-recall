namespace ClaudeRecall.Models;

public sealed class SearchResult
{
    public required SessionChain Chain { get; init; }
    public required List<SessionMatch> Matches { get; init; }
    public string? AiReason { get; set; }
    public double AiConfidence { get; set; }
    public bool? AiMatches { get; set; }
}

public sealed class SessionMatch
{
    public required SessionInfo Session { get; init; }
    public required List<MatchedMessage> Messages { get; init; }
    public int TotalMatches => Messages.Sum(m => m.MatchCount);
}

public sealed class MatchedMessage
{
    public required string Role { get; init; }
    public required string Text { get; init; }
    public required string Timestamp { get; init; }
    public required int MatchCount { get; init; }
    public required List<string> MatchedSnippets { get; init; }
}
