using ClaudeRecall.Models;
using Spectre.Console;

namespace ClaudeRecall.Tui;

public static class SearchView
{
    public static void RenderResults(List<SearchResult> results, string query)
    {
        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No matching sessions found.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Found {results.Count} matching session chain(s)[/]");
        AnsiConsole.WriteLine();

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

        var tree = new Tree(
            new Markup($"[blue bold]#{index}[/] [bold]{Markup.Escape(chain.Slug)}[/] " +
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

            var ts = session.FirstTimestamp?.ToString("yyyy-MM-dd HH:mm") ?? "?";
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
        var choices = new List<string>();

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var label = $"#{i + 1} {r.Chain.Slug} ({r.Matches.Sum(m => m.TotalMatches)} matches)";
            choices.Add(label);
        }

        choices.Add("[Exit]");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select a session chain to explore:[/]")
                .PageSize(15)
                .AddChoices(choices));

        if (selected == "[Exit]") return null;

        var idx = choices.IndexOf(selected);
        return idx >= 0 && idx < results.Count ? idx : null;
    }

    private static string ShortenPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith(home, StringComparison.Ordinal))
            return "~" + path[home.Length..];
        return path;
    }

    private static string FormatDateRange(DateTimeOffset? first, DateTimeOffset? last)
    {
        if (first is null) return "";
        if (last is null || first.Value.Date == last.Value.Date)
            return first.Value.ToString("yyyy-MM-dd");
        return $"{first.Value:yyyy-MM-dd} to {last.Value:yyyy-MM-dd}";
    }
}
