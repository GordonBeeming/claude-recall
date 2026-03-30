using Spectre.Console;

namespace ClaudeRecall.Tui;

public static class Theme
{
    public static readonly Style Matched = new(Color.Green, decoration: Decoration.Bold);
    public static readonly Style Dimmed = new(Color.Grey);
    public static readonly Style Highlight = new(Color.Yellow, decoration: Decoration.Bold);
    public static readonly Style UserRole = new(Color.Cyan1);
    public static readonly Style AssistantRole = new(Color.Green3);
    public static readonly Style ChainHeader = new(Color.Blue, decoration: Decoration.Bold);
    public static readonly Style SessionId = new(Color.Grey70);
    public static readonly Style Timestamp = new(Color.Grey50);
    public static readonly Style AiReason = new(Color.MediumPurple1);
}
