using System.Text.Json;
using System.Text.RegularExpressions;
using ClaudeRecall.Models;

namespace ClaudeRecall.Services;

public static class SearchEngine
{
    public static List<SearchResult> Search(
        List<SessionChain> chains,
        IReadOnlyList<string> patterns,
        int maxSnippetLength = 200)
    {
        var regexes = patterns
            .Select(p =>
            {
                try { return new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)); }
                catch { return null; }
            })
            .Where(r => r is not null)
            .ToList();

        if (regexes.Count == 0) return [];

        var results = new List<SearchResult>();
        var lockObj = new object();

        Parallel.ForEach(chains, chain =>
        {
            var chainMatches = new List<SessionMatch>();

            foreach (var session in chain.Sessions)
            {
                var sessionMessages = SearchSession(session, regexes!, maxSnippetLength);
                if (sessionMessages.Count > 0)
                {
                    chainMatches.Add(new SessionMatch
                    {
                        Session = session,
                        Messages = sessionMessages,
                    });
                }
            }

            if (chainMatches.Count > 0)
            {
                lock (lockObj)
                {
                    results.Add(new SearchResult
                    {
                        Chain = chain,
                        Matches = chainMatches,
                    });
                }
            }
        });

        return results
            .OrderByDescending(r => r.Chain.LastTimestamp ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static List<MatchedMessage> SearchSession(
        SessionInfo session,
        List<Regex> regexes,
        int maxSnippetLength)
    {
        var matches = new List<MatchedMessage>();

        try
        {
            foreach (var line in File.ReadLines(session.FilePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                SessionMessage? msg;
                try
                {
                    msg = JsonSerializer.Deserialize(line, SourceGenerationContext.Default.SessionMessage);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (msg is null) continue;

                var (role, text) = TextExtractor.Extract(msg);
                if (string.IsNullOrEmpty(text)) continue;

                var snippets = new List<string>();
                int totalCount = 0;

                foreach (var regex in regexes)
                {
                    try
                    {
                        var regMatches = regex.Matches(text);
                        if (regMatches.Count == 0) continue;

                        totalCount += regMatches.Count;

                        foreach (Match m in regMatches)
                        {
                            if (snippets.Count >= 3) break;

                            int start = Math.Max(0, m.Index - 40);
                            int end = Math.Min(text.Length, m.Index + m.Length + 40);
                            var snippet = text[start..end].ReplaceLineEndings(" ").Trim();

                            if (start > 0) snippet = "..." + snippet;
                            if (end < text.Length) snippet += "...";

                            if (snippet.Length > maxSnippetLength)
                                snippet = snippet[..maxSnippetLength] + "...";

                            snippets.Add(snippet);
                        }
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        // Skip slow patterns
                    }
                }

                if (totalCount > 0)
                {
                    matches.Add(new MatchedMessage
                    {
                        Role = role,
                        Text = text.Length > 500 ? text[..500] + "..." : text,
                        Timestamp = msg.Timestamp ?? "",
                        MatchCount = totalCount,
                        MatchedSnippets = snippets,
                    });
                }
            }
        }
        catch (IOException)
        {
            // File in use or disappeared
        }

        return matches;
    }
}
