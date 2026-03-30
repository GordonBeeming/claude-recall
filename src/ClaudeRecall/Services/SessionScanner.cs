using System.Text.Json;
using ClaudeRecall.Models;

namespace ClaudeRecall.Services;

public static class SessionScanner
{
    private static readonly string ClaudeProjectsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "projects");

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "claude-recall");

    private static readonly string CachePath = Path.Combine(CacheDir, "cache.json");

    public static List<SessionInfo> ScanAll()
    {
        if (!Directory.Exists(ClaudeProjectsDir))
            return [];

        var cache = LoadCache();
        var projectDirs = Directory.GetDirectories(ClaudeProjectsDir);
        var sessions = new List<SessionInfo>();
        var lockObj = new object();
        var cacheChanged = false;

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
                var sessionId = Path.GetFileNameWithoutExtension(file);
                var fileSize = new FileInfo(file).Length;
                var cacheKey = $"{Path.GetFileName(projectDir)}/{sessionId}";

                SessionInfo? info = null;

                if (cache.Entries.TryGetValue(cacheKey, out var cached) && cached.FileSize == fileSize)
                {
                    info = FromCache(cached, file, projectDir);
                }

                if (info is null)
                {
                    info = ScanSession(file, projectDir);
                    if (info is not null)
                    {
                        var entry = ToCache(info, fileSize);
                        lock (lockObj)
                        {
                            cache.Entries[cacheKey] = entry;
                            cacheChanged = true;
                        }
                    }
                }

                if (info is not null)
                {
                    lock (lockObj)
                    {
                        sessions.Add(info);
                    }
                }
            }
        });

        if (cacheChanged)
        {
            SaveCache(cache);
        }

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
                            if (string.IsNullOrWhiteSpace(text)) continue;

                            var cleaned = CleanUserMessage(text);
                            if (cleaned is not null)
                                firstUserMessage = cleaned.Length > 500 ? cleaned[..500] : cleaned;
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

    private static SessionInfo FromCache(SessionCacheEntry cached, string filePath, string projectDir)
    {
        return new SessionInfo
        {
            SessionId = cached.SessionId,
            FilePath = filePath,
            ProjectDir = projectDir,
            ProjectPath = cached.ProjectPath,
            Slug = cached.Slug,
            Cwd = cached.Cwd,
            FirstUserMessage = cached.FirstUserMessage,
            FirstTimestamp = cached.FirstTimestamp is not null ? DateTimeOffset.Parse(cached.FirstTimestamp) : null,
            LastTimestamp = cached.LastTimestamp is not null ? DateTimeOffset.Parse(cached.LastTimestamp) : null,
            MessageCount = cached.MessageCount,
        };
    }

    private static SessionCacheEntry ToCache(SessionInfo info, long fileSize)
    {
        return new SessionCacheEntry
        {
            SessionId = info.SessionId,
            FileSize = fileSize,
            ProjectPath = info.ProjectPath,
            Slug = info.Slug,
            Cwd = info.Cwd,
            FirstUserMessage = info.FirstUserMessage,
            FirstTimestamp = info.FirstTimestamp?.ToString("o"),
            LastTimestamp = info.LastTimestamp?.ToString("o"),
            MessageCount = info.MessageCount,
        };
    }

    private static SessionCache LoadCache()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                var json = File.ReadAllText(CachePath);
                return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.SessionCache) ?? new SessionCache();
            }
        }
        catch
        {
            // Corrupt cache, start fresh
        }

        return new SessionCache();
    }

    private static void SaveCache(SessionCache cache)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonSerializer.Serialize(cache, SourceGenerationContext.Default.SessionCache);
            File.WriteAllText(CachePath, json);
        }
        catch
        {
            // Non-critical, ignore
        }
    }

    /// <summary>
    /// Returns cleaned user message text, or null if it's a system/internal message to skip.
    /// For command invocations, returns the command as the user typed it (e.g. "/skill-name args").
    /// </summary>
    private static string? CleanUserMessage(string text)
    {
        var trimmed = text.TrimStart();

        if (!trimmed.StartsWith('<'))
            return trimmed;

        // Extract slash commands: <command-name>/foo</command-name> <command-args>bar</command-args>
        if (trimmed.Contains("<command-name>"))
        {
            var cmdName = ExtractTagContent(trimmed, "command-name");
            var cmdArgs = ExtractTagContent(trimmed, "command-args");

            if (cmdName is not null)
            {
                return cmdArgs is { Length: > 0 } ? $"{cmdName} {cmdArgs}" : cmdName;
            }
        }

        // Skip known internal prefixes
        string[] skipPrefixes =
        [
            "<local-command-caveat>",
            "<system-reminder>",
            "<user-prompt-submit-hook>",
        ];

        foreach (var prefix in skipPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return trimmed;
    }

    private static string? ExtractTagContent(string text, string tagName)
    {
        var open = $"<{tagName}>";
        var close = $"</{tagName}>";
        var start = text.IndexOf(open, StringComparison.Ordinal);
        if (start < 0) return null;
        start += open.Length;
        var end = text.IndexOf(close, start, StringComparison.Ordinal);
        if (end < 0) return null;
        return text[start..end].Trim();
    }

    private static string DecodeProjectPath(string dirName)
    {
        // Directory names encode paths: -Users-gordonbeeming-Developer -> /Users/gordonbeeming/Developer
        return "/" + dirName.TrimStart('-').Replace('-', '/');
    }
}
