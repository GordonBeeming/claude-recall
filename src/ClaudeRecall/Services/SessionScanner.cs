using System.Text.Json;
using ClaudeRecall.Models;

namespace ClaudeRecall.Services;

public static class SessionScanner
{
    private static readonly string ClaudeProjectsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "projects");

    public static List<SessionInfo> ScanAll()
    {
        if (!Directory.Exists(ClaudeProjectsDir))
            return [];

        var projectDirs = Directory.GetDirectories(ClaudeProjectsDir);
        var sessions = new List<SessionInfo>();
        var lockObj = new object();

        // Exclude sessions that are actively being written (e.g. the current Claude session
        // that launched claude-recall). A file modified in the last 10 seconds is likely active.
        var activeThreshold = DateTime.UtcNow.AddSeconds(-10);

        Parallel.ForEach(projectDirs, projectDir =>
        {
            var jsonlFiles = Directory.GetFiles(projectDir, "*.jsonl")
                .Where(f => !Path.GetFileName(f).StartsWith("agent-", StringComparison.Ordinal))
                .Where(f => File.GetLastWriteTimeUtc(f) < activeThreshold)
                .ToList();

            foreach (var file in jsonlFiles)
            {
                var info = ScanSession(file, projectDir);
                if (info is not null)
                {
                    lock (lockObj)
                    {
                        sessions.Add(info);
                    }
                }
            }
        });

        return sessions;
    }

    private static SessionInfo? ScanSession(string filePath, string projectDir)
    {
        var sessionId = Path.GetFileNameWithoutExtension(filePath);
        string? slug = null;
        string? cwd = null;
        string? firstUserMessage = null;
        DateTimeOffset? firstTs = null;
        DateTimeOffset? lastTs = null;
        int messageCount = 0;

        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var msg = JsonSerializer.Deserialize(line, SourceGenerationContext.Default.SessionMessage);
                    if (msg is null) continue;

                    if (msg.Timestamp is { } ts && DateTimeOffset.TryParse(ts, out var parsed))
                    {
                        firstTs ??= parsed;
                        lastTs = parsed;
                    }

                    slug ??= msg.Slug;
                    cwd ??= msg.Cwd;

                    if (msg.Type is "user" or "assistant")
                    {
                        messageCount++;
                        if (firstUserMessage is null && msg.Type == "user")
                        {
                            var (_, text) = TextExtractor.Extract(msg);
                            if (!string.IsNullOrWhiteSpace(text))
                                firstUserMessage = text.Length > 300 ? text[..300] : text;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                }
            }
        }
        catch (IOException)
        {
            return null;
        }

        if (messageCount == 0) return null;

        return new SessionInfo
        {
            SessionId = sessionId,
            FilePath = filePath,
            ProjectDir = projectDir,
            ProjectPath = DecodeProjectPath(Path.GetFileName(projectDir)),
            Slug = slug,
            Cwd = cwd,
            FirstUserMessage = firstUserMessage,
            FirstTimestamp = firstTs,
            LastTimestamp = lastTs,
            MessageCount = messageCount,
        };
    }

    private static string DecodeProjectPath(string dirName)
    {
        // Directory names encode paths: -Users-gordonbeeming-Developer -> /Users/gordonbeeming/Developer
        return "/" + dirName.TrimStart('-').Replace('-', '/');
    }
}
