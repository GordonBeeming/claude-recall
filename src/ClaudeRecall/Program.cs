using System.Reflection;
using ClaudeRecall.Models;
using ClaudeRecall.Services;
using ClaudeRecall.Tui;
using Spectre.Console;

// Parse args manually to avoid Spectre.Console.Cli reflection issues with AOT
var query = "";
var regexMode = false;
var noAi = false;
var allProjects = false;
var days = 7;

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
        case "--all-projects":
            allProjects = true;
            break;
        case "--days" when i + 1 < args.Length && int.TryParse(args[i + 1], out var d):
            days = d;
            i++;
            break;
        case "--help" or "-h":
            AnsiConsole.MarkupLine("[bold]claude-recall[/] — Search your Claude Code session history");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Usage:[/] claude-recall <query> <options>");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Options:[/]");
            AnsiConsole.MarkupLine("  --regex         Raw regex search (skip AI term generation)");
            AnsiConsole.MarkupLine("  --no-ai         Skip all AI features");
            AnsiConsole.MarkupLine("  --all-projects  Search all projects (default: current project only)");
            AnsiConsole.MarkupLine("  --days N        Search last N days (default: 7)");
            AnsiConsole.MarkupLine("  --help, -h      Show this help");
            AnsiConsole.MarkupLine("  --version       Show version information");
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

if (!allProjects)
{
    var cwd = Environment.CurrentDirectory;
    sessions = sessions
        .Where(s =>
        {
            var projectPath = s.ProjectPath ?? s.ProjectDir;
            return cwd.StartsWith(projectPath, StringComparison.Ordinal);
        })
        .ToList();
}

var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
sessions = sessions
    .Where(s => (s.LastTimestamp ?? s.FirstTimestamp ?? DateTimeOffset.MinValue) >= cutoff)
    .ToList();

AnsiConsole.MarkupLine($"[grey]Found {sessions.Count} sessions across {sessions.Select(s => s.ProjectDir).Distinct().Count()} projects (last {days} days)[/]");

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
        var firstUserMsg = r.Chain.Sessions
            .Select(s => s.FirstUserMessage)
            .FirstOrDefault(m => m is not null);
        var snippets = r.Matches
            .SelectMany(m => m.Messages)
            .SelectMany(m => m.MatchedSnippets)
            .Take(5);
        var context = firstUserMsg is not null
            ? $"First user message: {firstUserMsg}\n\nMatched snippets:\n{string.Join("\n", snippets)}"
            : string.Join("\n", snippets);
        return (r.Chain.Sessions[0].SessionId, context);
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
                result.AiMatches = v.Matches;
            }
        }

        // Remove results that AI explicitly rejected, keep unvalidated ones (beyond top 20)
        results = results
            .Where(r => r.AiMatches != false)
            .OrderByDescending(r => r.AiConfidence > 0 ? 1 : 0)
            .ThenByDescending(r => r.AiConfidence)
            .ThenByDescending(r => r.Chain.LastTimestamp ?? DateTimeOffset.MinValue)
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
