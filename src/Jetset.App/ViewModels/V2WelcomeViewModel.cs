namespace Jetset.App.ViewModels;

public sealed record ShortcutItem(string Shortcut, string Action);

public sealed class V2WelcomeViewModel
{
    public V2WelcomeViewModel(bool upgradedFromV1)
    {
        UpgradedFromV1 = upgradedFromV1;
        Title = upgradedFromV1 ? "Welcome to Jetset V2" : "Welcome to Jetset";
        IntroText = upgradedFromV1
            ? "Your existing work sessions were linked to tasks automatically. Nothing was lost."
            : "Jetset is now a personal productivity workspace built around focused work sessions.";
    }

    public bool UpgradedFromV1 { get; }

    public string Title { get; }

    public string IntroText { get; }

    public IReadOnlyList<string> Highlights { get; } =
    [
        "Manage quick tasks, projects, and milestones from the navigation tabs.",
        "Capture working context when pausing, switching, or finishing.",
        "Resume paused work from the waiting queue on the Focus view.",
        "Review focus time, activity heatmaps, and project momentum in Analytics."
    ];

    public IReadOnlyList<ShortcutItem> Shortcuts => DefaultShortcuts;

    public static IReadOnlyList<ShortcutItem> DefaultShortcuts { get; } =
    [
        new("Ctrl+N", "Start a new work session"),
        new("Ctrl+P", "Pause or resume the active session"),
        new("Ctrl+Enter", "Finish the active session"),
        new("Ctrl+M", "Toggle compact mode"),
        new("Ctrl+H", "Show or hide the main window")
    ];
}
