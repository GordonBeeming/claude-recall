namespace ClaudeRecall.Models;

public sealed class SessionChain
{
    public required string Slug { get; init; }
    public required string ProjectDir { get; init; }
    public string? ProjectPath { get; init; }
    public required List<SessionInfo> Sessions { get; init; }

    public DateTimeOffset? FirstTimestamp => Sessions.FirstOrDefault()?.FirstTimestamp;
    public DateTimeOffset? LastTimestamp => Sessions.LastOrDefault()?.LastTimestamp;
    public int TotalMessages => Sessions.Sum(s => s.MessageCount);
}
