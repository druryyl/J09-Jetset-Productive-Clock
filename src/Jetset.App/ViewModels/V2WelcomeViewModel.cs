namespace Jetset.App.ViewModels;

public sealed record ShortcutItem(string Shortcut, string Action);

public sealed class V2WelcomeViewModel
{
    public V2WelcomeViewModel(bool upgradedFromV1)
    {
        UpgradedFromV1 = upgradedFromV1;
        Title = upgradedFromV1 ? "Welcome to Jetset V2" : "Welcome to Jetset";
        IntroText = upgradedFromV1
            ? "Your existing work sessions were linked to tasks automatically. Jetset is now a Work Tree workspace — organize in a tree, run one task at a time, and keep project context in the panel beside it."
            : "Jetset is a personal execution workspace. Capture tasks at the tree root, organize work in projects, run one task at a time, and keep project context visible while you work.";
    }

    public bool UpgradedFromV1 { get; }

    public string Title { get; }

    public string IntroText { get; }

    public IReadOnlyList<string> Highlights { get; } =
    [
        "Work Tree is your primary workspace — projects, tasks, drag-drop, and quick capture at the root.",
        "Quick Capture to Inbox without disturbing your Running task (Ctrl+Shift+C).",
        "One Running task at a time — use the Running Task Bar for timer, Done, Waiting, and Pause.",
        "Project context, deadlines, and effort rollup live in the Context Panel beside the tree.",
        "Review focus time, heatmaps, and streaks in Analytics (Settings tab).",
        "Press Ctrl+M for a compact timer overlay when you want minimal chrome."
    ];

    public IReadOnlyList<ShortcutItem> Shortcuts => DefaultShortcuts;

    public static IReadOnlyList<ShortcutItem> DefaultShortcuts { get; } =
    [
        new("Ctrl+Shift+C", "Quick Capture to Inbox at tree root"),
        new("Ctrl+N", "Start work (opens compact overlay task picker)"),
        new("Ctrl+P", "Pause or resume the active session"),
        new("Ctrl+Enter", "Finish the active session"),
        new("Ctrl+M", "Toggle compact overlay"),
        new("Ctrl+H", "Show or hide the main window")
    ];
}
