using System.IO;

namespace Jetset.Tests;

public class ProjectsViewTests
{
    [Fact]
    public void ProjectsViewXaml_HasNoMomentumOrAnalyticsSections()
    {
        var content = File.ReadAllText(FindRepoFile(@"src\Jetset.App\Views\ProjectsView.xaml"));

        Assert.DoesNotContain("Momentum", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MomentumWeek", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WeeklyFocus", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WeeklyCompletion", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GetProjectMomentum", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectsViewXaml_ShowsProjectContextAndTasks()
    {
        var content = File.ReadAllText(FindRepoFile(@"src\Jetset.App\Views\ProjectsView.xaml"));

        Assert.Contains("Project context", content, StringComparison.Ordinal);
        Assert.Contains("ContextText", content, StringComparison.Ordinal);
        Assert.Contains("ProjectTasks", content, StringComparison.Ordinal);
        Assert.Contains("Start work", content, StringComparison.Ordinal);
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

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }
}
