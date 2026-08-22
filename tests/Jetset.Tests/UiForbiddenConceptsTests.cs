using System.IO;

namespace Jetset.Tests;

/// <summary>
/// Ensures primary UI surfaces do not reference removed DOMAIN concepts.
/// </summary>
public class UiForbiddenConceptsTests
{
    private static readonly string[] ForbiddenTerms =
    [
        "Milestone",
        "ContextSnapshot",
        "ResumeQueue",
        "Momentum",
        "TaskSwitchEvent"
    ];

    private static readonly string[] ViewFiles =
    [
        "MainWindow.xaml",
        "Views/WorkTreeView.xaml",
        "Views/ContextPanelView.xaml",
        "Views/RunningTaskBarView.xaml",
        "Views/CompactOverlayView.xaml",
        "Views/SettingsView.xaml",
        "Views/AnalyticsView.xaml",
        "Views/V2WelcomeDialog.xaml"
    ];

    [Fact]
    public void PrimaryViews_HaveNoForbiddenConceptLabels()
    {
        foreach (var file in ViewFiles)
        {
            var path = FindRepoFile(Path.Combine("src", "Jetset.App", file));
            var content = File.ReadAllText(path);

            foreach (var term in ForbiddenTerms)
            {
                Assert.DoesNotContain(term, content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void DemotedPrimaryViews_WereRemoved()
    {
        var viewsDir = FindRepoDirectory(Path.Combine("src", "Jetset.App", "Views"));
        Assert.False(File.Exists(Path.Combine(viewsDir, "FocusView.xaml")));
        Assert.False(File.Exists(Path.Combine(viewsDir, "TasksView.xaml")));
        Assert.False(File.Exists(Path.Combine(viewsDir, "ProjectsView.xaml")));
        Assert.False(File.Exists(Path.Combine(viewsDir, "SettingsWindow.xaml")));
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException($"Could not locate repo file: {relativePath}");
    }

    private static string FindRepoDirectory(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException($"Could not locate repo directory: {relativePath}");
    }
}
