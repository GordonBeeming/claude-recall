using System.Text.Json.Serialization;

namespace ClaudeRecall.Models;

public sealed class SessionCacheEntry
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("projectPath")]
    public string? ProjectPath { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("firstUserMessage")]
    public string? FirstUserMessage { get; set; }

    [JsonPropertyName("firstTimestamp")]
    public string? FirstTimestamp { get; set; }

    [JsonPropertyName("lastTimestamp")]
    public string? LastTimestamp { get; set; }

    [JsonPropertyName("messageCount")]
    public int MessageCount { get; set; }

    [JsonPropertyName("aiSummary")]
    public string? AiSummary { get; set; }
}

public sealed class SessionCache
{
    [JsonPropertyName("entries")]
    public Dictionary<string, SessionCacheEntry> Entries { get; set; } = new();
}
