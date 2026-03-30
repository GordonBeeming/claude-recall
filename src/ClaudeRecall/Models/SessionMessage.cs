using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeRecall.Models;

/// <summary>
/// Represents a single line from a session JSONL file.
/// Uses flexible JsonElement for nested content since the schema varies by message type.
/// </summary>
public sealed class SessionMessage
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("message")]
    public JsonElement? Message { get; set; }

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("lastPrompt")]
    public string? LastPrompt { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("parentUuid")]
    public string? ParentUuid { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    [JsonPropertyName("toolUseResult")]
    public JsonElement? ToolUseResult { get; set; }
}

[JsonSerializable(typeof(SessionMessage))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(AiSearchTermsResponse))]
[JsonSerializable(typeof(AiValidationResponse))]
[JsonSerializable(typeof(AiSessionCandidate))]
[JsonSerializable(typeof(SessionCache))]
[JsonSerializable(typeof(SessionCacheEntry))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class SourceGenerationContext : JsonSerializerContext;
