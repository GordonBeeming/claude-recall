using System.Text;
using System.Text.Json;
using ClaudeRecall.Models;

namespace ClaudeRecall.Services;

public static class TextExtractor
{
    public static (string role, string text) Extract(SessionMessage msg)
    {
        return msg.Type switch
        {
            "user" => ("user", ExtractFromMessage(msg.Message)),
            "assistant" => ("assistant", ExtractFromMessage(msg.Message)),
            "last-prompt" => ("user", msg.LastPrompt ?? ""),
            "queue-operation" => ("user", ExtractContent(msg.Content)),
            _ => ("system", ""),
        };
    }

    private static string ExtractFromMessage(JsonElement? message)
    {
        if (message is not { } msg) return "";

        if (msg.ValueKind != JsonValueKind.Object) return "";

        if (!msg.TryGetProperty("content", out var content)) return "";

        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? "";

        if (content.ValueKind != JsonValueKind.Array) return "";

        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object) continue;

            var blockType = block.TryGetProperty("type", out var t) ? t.GetString() : null;

            switch (blockType)
            {
                case "text":
                    if (block.TryGetProperty("text", out var text))
                        sb.AppendLine(text.GetString());
                    break;

                case "thinking":
                    if (block.TryGetProperty("thinking", out var thinking))
                    {
                        var thinkText = thinking.GetString();
                        if (!string.IsNullOrEmpty(thinkText))
                            sb.AppendLine(thinkText);
                    }
                    break;

                case "tool_use":
                    if (block.TryGetProperty("name", out var toolName))
                        sb.AppendLine($"[tool: {toolName.GetString()}]");
                    if (block.TryGetProperty("input", out var input))
                        ExtractToolInput(input, sb);
                    break;

                case "tool_result":
                    if (block.TryGetProperty("content", out var resultContent))
                        ExtractToolResultContent(resultContent, sb);
                    break;
            }
        }

        return sb.ToString();
    }

    private static void ExtractToolInput(JsonElement input, StringBuilder sb)
    {
        if (input.ValueKind != JsonValueKind.Object) return;

        // Extract common searchable fields from tool inputs
        string[] searchableFields = ["prompt", "command", "description", "query", "pattern", "content", "text", "message"];

        foreach (var field in searchableFields)
        {
            if (input.TryGetProperty(field, out var val) && val.ValueKind == JsonValueKind.String)
            {
                var s = val.GetString();
                if (!string.IsNullOrEmpty(s))
                    sb.AppendLine(s);
            }
        }
    }

    private static void ExtractToolResultContent(JsonElement content, StringBuilder sb)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            sb.AppendLine(content.GetString());
            return;
        }

        if (content.ValueKind != JsonValueKind.Array) return;

        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("type", out var itemType) &&
                itemType.GetString() == "text" &&
                item.TryGetProperty("text", out var itemText))
            {
                sb.AppendLine(itemText.GetString());
            }
        }
    }

    private static string ExtractContent(JsonElement? content)
    {
        if (content is not { } c) return "";
        return c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
    }
}
