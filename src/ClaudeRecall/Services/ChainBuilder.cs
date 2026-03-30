using ClaudeRecall.Models;

namespace ClaudeRecall.Services;

public static class ChainBuilder
{
    public static List<SessionChain> Build(List<SessionInfo> sessions)
    {
        var groups = sessions
            .GroupBy(s => (s.ProjectDir, Slug: s.Slug ?? s.SessionId))
            .Select(g => new SessionChain
            {
                Slug = g.Key.Slug,
                ProjectDir = g.Key.ProjectDir,
                ProjectPath = g.First().ProjectPath,
                Sessions = g.OrderBy(s => s.FirstTimestamp ?? DateTimeOffset.MaxValue).ToList(),
            })
            .OrderByDescending(c => c.LastTimestamp ?? DateTimeOffset.MinValue)
            .ToList();

        return groups;
    }
}
