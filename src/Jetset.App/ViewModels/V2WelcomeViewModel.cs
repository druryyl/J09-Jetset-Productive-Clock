namespace Jetset.App.ViewModels;

public sealed record ShortcutItem(string Shortcut, string Action);

public sealed class V2WelcomeViewModel
{
    public V2WelcomeViewModel(bool upgradedFromV1)
    {
        UpgradedFromV1 = upgradedFromV1;
        Title = upgradedFromV1 ? "Welcome to Jetset V2" : "Welcome to Jetset";
        IntroText = upgradedFromV1
            ? "Your existing work sessions were linked to tasks automatically. Jetset is now a personal execution workspace — task-first, with one Running task at a time."
            : "Jetset is a personal execution workspace. Capture tasks quickly, run one at a time, and keep project context across switches.";
    }

    public bool UpgradedFromV1 { get; }

    public string Title { get; }

    public string IntroText { get; }

    public IReadOnlyList<string> Highlights { get; } =
    [
        "Quick Capture to Inbox without disturbing your Running task.",
        "One Running task at a time — switch from Ready or mark the previous task as Waiting.",
        "Project context lives on the project, not on individual tasks.",
        "Review focus time, activity heatmaps, and streaks in Analytics."
    ];

    public IReadOnlyList<ShortcutItem> Shortcuts => DefaultShortcuts;

    public static IReadOnlyList<ShortcutItem> DefaultShortcuts { get; } =
    [
        new("Ctrl+Shift+C", "Quick Capture to Inbox"),
        new("Ctrl+N", "Start work on the selected task"),
        new("Ctrl+P", "Pause or resume the active session"),
        new("Ctrl+Enter", "Finish the active session"),
        new("Ctrl+M", "Toggle compact mode"),
        new("Ctrl+H", "Show or hide the main window")
    ];
}
