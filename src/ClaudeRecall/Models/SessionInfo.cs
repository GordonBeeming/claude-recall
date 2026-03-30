namespace ClaudeRecall.Models;

public sealed class SessionInfo
{
    public required string SessionId { get; init; }
    public required string FilePath { get; init; }
    public required string ProjectDir { get; init; }
    public string? ProjectPath { get; init; }
    public string? Slug { get; init; }
    public string? Cwd { get; init; }
    public string? FirstUserMessage { get; init; }
    public DateTimeOffset? FirstTimestamp { get; init; }
    public DateTimeOffset? LastTimestamp { get; init; }
    public int MessageCount { get; init; }
    public string? AiSummary { get; set; }
}
