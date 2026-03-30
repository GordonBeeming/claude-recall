using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClaudeRecall.Models;

namespace ClaudeRecall.Services;

public static class ClaudeAiService
{
    public static async Task<List<string>> GenerateSearchTerms(string userQuery)
    {
        var prompt = $$"""
        The user wants to find a past Claude Code conversation session. Their description:
        "{{userQuery}}"

        Generate 5-10 regex patterns (case-insensitive) that would help find this session in conversation logs.
        Include variations: abbreviations, synonyms, related terms.
        Keep patterns simple and broad enough to catch relevant discussions.

        Return ONLY a JSON object with a "patterns" array of strings. No explanation.
        Example: {"patterns": ["financ", "iphone|ios|mobile", "budget|money|expense", "swift|swiftui", "app.*(money|budget)"]}
        """;

        var result = await RunClaude(prompt);
        if (string.IsNullOrEmpty(result)) return FallbackPatterns(userQuery);

        try
        {
            // Try to extract JSON from the response
            var jsonStart = result.IndexOf('{');
            var jsonEnd = result.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = result[jsonStart..(jsonEnd + 1)];
                var response = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.AiSearchTermsResponse);
                if (response?.Patterns is { Count: > 0 })
                    return response.Patterns;
            }
        }
        catch (JsonException)
        {
            // Fall through to fallback
        }

        return FallbackPatterns(userQuery);
    }

    public static async Task<List<AiSessionCandidate>> ValidateCandidates(
        string userQuery,
        List<(string sessionId, string summary)> candidates)
    {
        if (candidates.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLine($"The user is looking for a session about: \"{userQuery}\"");
        sb.AppendLine();
        sb.AppendLine("Here are candidate sessions with text snippets. For each, determine if it matches the user's intent.");
        sb.AppendLine();

        foreach (var (id, summary) in candidates.Take(20))
        {
            sb.AppendLine($"--- Session {id} ---");
            sb.AppendLine(summary.Length > 1000 ? summary[..1000] + "..." : summary);
            sb.AppendLine();
        }

        sb.AppendLine("""
        Return ONLY a JSON object with a "results" array. Each element:
        {"sessionId": "...", "matches": true/false, "confidence": 0.0-1.0, "reason": "brief reason"}
        """);

        var result = await RunClaude(sb.ToString());
        if (string.IsNullOrEmpty(result)) return [];

        try
        {
            var jsonStart = result.IndexOf('{');
            var jsonEnd = result.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = result[jsonStart..(jsonEnd + 1)];
                var response = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.AiValidationResponse);
                return response?.Results ?? [];
            }
        }
        catch (JsonException)
        {
            // Return empty on parse failure
        }

        return [];
    }

    private static async Task<string?> RunClaude(string prompt)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "claude",
                ArgumentList = { "-p", prompt, "--output-format", "text" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> FallbackPatterns(string userQuery)
    {
        // Split query into words, make each a pattern
        return userQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2)
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}
