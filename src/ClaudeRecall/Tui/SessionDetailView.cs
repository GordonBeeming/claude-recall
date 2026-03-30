using System.Diagnostics;
using ClaudeRecall.Models;
using Spectre.Console;

namespace ClaudeRecall.Tui;

public static class SessionDetailView
{
    public static void Show(SearchResult result)
    {
        var chain = result.Chain;

        AnsiConsole.Write(new Rule($"[blue bold]{Markup.Escape(chain.Slug)}[/]").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]Project: {Markup.Escape(chain.ProjectPath ?? chain.ProjectDir)}[/]");
        AnsiConsole.MarkupLine($"[grey]Sessions in chain: {chain.Sessions.Count}[/]");
        AnsiConsole.WriteLine();

        foreach (var match in result.Matches)
        {
            var idx = chain.Sessions.IndexOf(match.Session) + 1;
            AnsiConsole.Write(new Rule(
                $"[green bold]Session {idx}/{chain.Sessions.Count}[/] [grey]{match.Session.SessionId}[/]")
                .LeftJustified());

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Role[/]").Width(10))
                .AddColumn(new TableColumn("[bold]Timestamp[/]").Width(20))
                .AddColumn(new TableColumn("[bold]Content[/]"));

            foreach (var msg in match.Messages)
            {
                var roleColor = msg.Role == "user" ? "cyan1" : "green3";
                var ts = !string.IsNullOrEmpty(msg.Timestamp) && DateTimeOffset.TryParse(msg.Timestamp, out var parsed)
                    ? parsed.ToString("HH:mm:ss")
                    : "?";

                var content = string.Join("\n", msg.MatchedSnippets);
                if (string.IsNullOrEmpty(content))
                    content = msg.Text.Length > 300 ? msg.Text[..300] + "..." : msg.Text;

                table.AddRow(
                    new Markup($"[{roleColor}]{msg.Role}[/]"),
                    new Markup($"[grey50]{ts}[/]"),
                    new Markup(Markup.Escape(content)));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        PromptSessionAction(result);
    }

    private static void PromptSessionAction(SearchResult result)
    {
        var chain = result.Chain;
        var choices = new List<string>();

        if (result.Matches.Count > 0)
        {
            var firstMatch = result.Matches[0].Session;
            choices.Add($"Resume matched session ({firstMatch.SessionId[..8]}...)");
        }

        if (chain.Sessions.Count > 1)
        {
            var latest = chain.Sessions[^1];
            choices.Add($"Resume latest in chain ({latest.SessionId[..8]}...)");
        }

        choices.Add("Copy session ID to clipboard");
        choices.Add("Back to results");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Action:[/]")
                .AddChoices(choices));

        if (selected.StartsWith("Resume matched"))
        {
            ResumeSession(result.Matches[0].Session.SessionId);
        }
        else if (selected.StartsWith("Resume latest"))
        {
            ResumeSession(chain.Sessions[^1].SessionId);
        }
        else if (selected.StartsWith("Copy"))
        {
            var id = result.Matches.Count > 0
                ? result.Matches[0].Session.SessionId
                : chain.Sessions[^1].SessionId;

            // Use pbcopy on macOS
            CopyToClipboard(id);
            AnsiConsole.MarkupLine($"[green]Copied {id} to clipboard[/]");
        }
    }

    private static void ResumeSession(string sessionId)
    {
        AnsiConsole.MarkupLine($"[yellow]Launching claude --resume {sessionId}...[/]");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "claude",
                ArgumentList = { "--resume", sessionId },
                UseShellExecute = false,
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to launch claude: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pbcopy",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return;

            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit(2000);
        }
        catch
        {
            // Clipboard not available
            AnsiConsole.MarkupLine($"[grey]Session ID: {text}[/]");
        }
    }
}
