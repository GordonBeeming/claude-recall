using System.Reflection;
using ClaudeRecall.Models;
using ClaudeRecall.Services;
using ClaudeRecall.Tui;
using Spectre.Console;

// Parse args manually to avoid Spectre.Console.Cli reflection issues with AOT
var query = "";
var regexMode = false;
var noAi = false;
string? projectFilter = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--regex":
            regexMode = true;
            break;
        case "--no-ai":
            noAi = true;
            break;
        case "--project" when i + 1 < args.Length:
            projectFilter = args[++i];
            break;
        case "--help" or "-h":
            AnsiConsole.MarkupLine("[bold]claude-recall[/] — Search your Claude Code session history");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Usage:[/] claude-recall <query> <options>");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Options:[/]");
            AnsiConsole.MarkupLine("  --regex       Raw regex search (skip AI term generation)");
            AnsiConsole.MarkupLine("  --no-ai       Skip all AI features");
            AnsiConsole.MarkupLine("  --project X   Filter to sessions in projects matching X");
            AnsiConsole.MarkupLine("  --help, -h    Show this help");
            AnsiConsole.MarkupLine("  --version     Show version information");
            return 0;
        case "--version":
            var infoVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
            // Strip the +commithash suffix that .NET appends
            var version = infoVersion.Split('+')[0];
            AnsiConsole.MarkupLine($"claude-recall {version}");
            return 0;
        default:
            if (!args[i].StartsWith('-'))
                query = args[i];
            break;
    }
}

if (string.IsNullOrWhiteSpace(query))
{
    query = AnsiConsole.Ask<string>("[bold]Search your Claude sessions:[/]");
}

if (string.IsNullOrWhiteSpace(query))
{
    AnsiConsole.MarkupLine("[red]No query provided.[/]");
    return 1;
}

// Step 1: Scan sessions
List<SessionInfo> sessions = [];
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("[blue]Scanning sessions...[/]", async _ =>
    {
        await Task.Yield();
        sessions = SessionScanner.ScanAll();
    });

if (projectFilter is { Length: > 0 })
{
    sessions = sessions
        .Where(s => (s.ProjectPath ?? s.ProjectDir)
            .Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
        .ToList();
}

AnsiConsole.MarkupLine($"[grey]Found {sessions.Count} sessions across {sessions.Select(s => s.ProjectDir).Distinct().Count()} projects[/]");

// Step 2: Build chains
var chains = ChainBuilder.Build(sessions);
AnsiConsole.MarkupLine($"[grey]{chains.Count} session chains[/]");

// Step 3: Generate search patterns
List<string> patterns;

if (regexMode || noAi)
{
    patterns = [query];
    AnsiConsole.MarkupLine($"[grey]Regex mode: searching for \"{Markup.Escape(query)}\"[/]");
}
else
{
    patterns = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("[blue]AI generating search terms...[/]", async _ =>
            await ClaudeAiService.GenerateSearchTerms(query));

    AnsiConsole.MarkupLine($"[grey]Search patterns: {string.Join(", ", patterns.Select(p => Markup.Escape(p)))}[/]");
}

// Step 4: Search
List<SearchResult> results = [];

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("[blue]Searching sessions...[/]", async _ =>
    {
        await Task.Yield();
        results = SearchEngine.Search(chains, patterns);
    });

AnsiConsole.MarkupLine($"[grey]{results.Count} matching chains[/]");

// Step 5: AI validation (if enabled and we have results to narrow down)
if (!regexMode && !noAi && results.Count > 3)
{
    var candidates = results.Take(20).Select(r =>
    {
        var snippets = r.Matches
            .SelectMany(m => m.Messages)
            .SelectMany(m => m.MatchedSnippets)
            .Take(5);
        return (r.Chain.Sessions[0].SessionId, string.Join("\n", snippets));
    }).ToList();

    var validated = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("[blue]AI validating matches...[/]", async _ =>
            await ClaudeAiService.ValidateCandidates(query, candidates));

    if (validated.Count > 0)
    {
        var validatedMap = validated.ToDictionary(v => v.SessionId, v => v);

        foreach (var result in results)
        {
            var firstSession = result.Chain.Sessions[0].SessionId;
            if (validatedMap.TryGetValue(firstSession, out var v))
            {
                result.AiReason = v.Reason;
                result.AiConfidence = v.Confidence;
            }
        }

        results = results
            .OrderByDescending(r => r.AiConfidence > 0 ? 1 : 0)
            .ThenByDescending(r => r.AiConfidence)
            .ThenByDescending(r => r.Matches.Sum(m => m.TotalMatches))
            .ToList();
    }
}

AnsiConsole.WriteLine();

// Step 6: Display results
SearchView.RenderResults(results, query);

// Step 7: Interactive selection
while (true)
{
    var selected = SearchView.PromptForAction(results);
    if (selected is null) break;

    AnsiConsole.Clear();
    SessionDetailView.Show(results[selected.Value]);
    AnsiConsole.WriteLine();
}

return 0;
