using ClaudeRecall.Models;
using Spectre.Console;

namespace ClaudeRecall.Tui;

public static class SearchView
{
    public static void RenderResults(List<SearchResult> results, string query, bool verbose)
    {
        if (!verbose) return;

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            RenderChainTree(result, i + 1);
            AnsiConsole.WriteLine();
        }
    }

    private static void RenderChainTree(SearchResult result, int index)
    {
        var chain = result.Chain;
        var matchedIds = new HashSet<string>(result.Matches.Select(m => m.Session.SessionId));
        var totalMatches = result.Matches.Sum(m => m.TotalMatches);

        var projectLabel = ShortenPath(chain.ProjectPath ?? chain.ProjectDir);
        var dateRange = FormatDateRange(chain.FirstTimestamp, chain.LastTimestamp);
        var title = GetSessionTitle(chain);

        var tree = new Tree(
            new Markup($"[blue bold]#{index}[/] [bold]{Markup.Escape(title)}[/] " +
                       $"[grey]({projectLabel})[/] " +
                       $"[grey50]{dateRange}[/] " +
                       $"[green]{totalMatches} match(es)[/]"));

        if (result.AiReason is { Length: > 0 })
        {
            tree.AddNode(new Markup($"[mediumpurple1]AI: {Markup.Escape(result.AiReason)}[/]"));
        }

        for (int i = 0; i < chain.Sessions.Count; i++)
        {
            var session = chain.Sessions[i];
            var isMatched = matchedIds.Contains(session.SessionId);
            var position = $"{i + 1}/{chain.Sessions.Count}";

            var match = result.Matches.FirstOrDefault(m => m.Session.SessionId == session.SessionId);
            var matchInfo = match is not null ? $" ({match.TotalMatches} matches)" : "";

            var ts = FormatFriendlyDate(session.FirstTimestamp);
            var msgs = session.MessageCount;
            var escapedPosition = Markup.Escape($"[{position}]");

            string label;
            if (isMatched)
            {
                label = $"[green bold]>>> {escapedPosition} {Markup.Escape(session.SessionId)}[/] " +
                        $"[grey50]{ts}[/] [grey]({msgs} msgs)[/] [green]{Markup.Escape(matchInfo)}[/]";
            }
            else
            {
                label = $"[grey]    {escapedPosition} {Markup.Escape(session.SessionId)}[/] " +
                        $"[grey50]{ts}[/] [grey]({msgs} msgs)[/]";
            }

            var node = tree.AddNode(new Markup(label));

            // Show snippets for matched sessions
            if (match is not null)
            {
                foreach (var msg in match.Messages.Take(3))
                {
                    var roleColor = msg.Role == "user" ? "cyan1" : "green3";
                    foreach (var snippet in msg.MatchedSnippets.Take(2))
                    {
                        node.AddNode(new Markup(
                            $"[{roleColor}]{msg.Role}:[/] [grey]{Markup.Escape(snippet)}[/]"));
                    }
                }
            }
        }

        AnsiConsole.Write(tree);
    }

    public static int? PromptForAction(List<SearchResult> results)
    {
        var choiceMap = new Dictionary<string, int>();
        var choiceList = new List<string>();

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var chain = r.Chain;
            var title = GetSessionTitle(chain);
            var first = FormatFriendlyDate(chain.FirstTimestamp);
            var last = FormatFriendlyDate(chain.LastTimestamp);
            var dateInfo = chain.FirstTimestamp?.Date == chain.LastTimestamp?.Date
                ? first
                : $"{first} - {last}";

            var line1 = $"#{i + 1} {Markup.Escape(title)}  [grey]{dateInfo}  ({chain.TotalMessages} msgs)[/]";

            var description = GetChainDescription(chain, 200);
            var line2 = description is not null ? $"   [grey]{Markup.Escape(description)}[/]" : "";

            var aiReason = r.AiReason is { Length: > 0 } ? $"   [mediumpurple1]AI: {Markup.Escape(r.AiReason)}[/]" : "";

            var parts = new List<string> { line1 };
            if (line2.Length > 0) parts.Add(line2);
            if (aiReason.Length > 0) parts.Add(aiReason);

            var label = string.Join("\n", parts);
            choiceList.Add(label);
            choiceMap[label] = i;
        }

        var exitLabel = Markup.Escape("[Exit]");
        choiceList.Add(exitLabel);

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select a session to explore:[/]")
                .PageSize(15)
                .AddChoices(choiceList));

        if (selected == exitLabel) return null;

        return choiceMap.TryGetValue(selected, out var idx) ? idx : null;
    }

    private static string GetSessionTitle(SessionChain chain)
    {
        // Prefer AI summary, fall back to slug
        var aiSummary = chain.Sessions
            .Select(s => s.AiSummary)
            .FirstOrDefault(s => s is not null);

        return aiSummary ?? chain.Slug;
    }

    private static string? GetChainDescription(SessionChain chain, int maxLength = 80)
    {
        var firstMsg = chain.Sessions
            .Select(s => s.FirstUserMessage)
            .FirstOrDefault(m => m is not null);

        if (firstMsg is null) return null;

        var firstLine = firstMsg.ReplaceLineEndings(" ").Trim();
        return firstLine.Length > maxLength ? firstLine[..maxLength] + "..." : firstLine;
    }

    private static string ShortenPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith(home, StringComparison.Ordinal))
            return "~" + path[home.Length..];
        return path;
    }

    private static string FormatFriendlyDate(DateTimeOffset? dt)
    {
        if (dt is null) return "?";
        var local = dt.Value.ToLocalTime();
        var format = local.Year == DateTimeOffset.Now.Year ? "dd MMM HH:mm" : "dd MMM yyyy HH:mm";
        return local.ToString(format);
    }

    private static string FormatDateRange(DateTimeOffset? first, DateTimeOffset? last)
    {
        if (first is null) return "";
        if (last is null || first.Value.Date == last.Value.Date)
            return FormatFriendlyDate(first);
        return $"{FormatFriendlyDate(first)} to {FormatFriendlyDate(last)}";
    }
}
