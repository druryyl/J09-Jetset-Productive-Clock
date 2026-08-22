using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;

namespace Jetset.Tests;

public class WorkTreeServiceTests
{
    private static (
        WorkTreeService WorkTree,
        TaskService Tasks,
        ProjectService Projects)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var taskStore = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => taskStore.List());
        var tasks = new TaskService(taskStore, projectStore, () => now);
        var projects = new ProjectService(projectStore, taskStore, () => now);
        var workTree = new WorkTreeService(taskStore, projectStore);
        return (workTree, tasks, projects);
    }

    [Fact]
    public void ListRootWorkItems_ReturnsProjectsAndStandaloneTasks()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (workTree, tasks, projects) = CreateHarness(start);

        var project = projects.Create("Jetset V2");
        var standalone = tasks.Create("Inbox capture");
        tasks.Create("Child task", project.Id);

        var roots = workTree.ListRootWorkItems();

        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, n => n.Kind == WorkItemKind.Project && n.Id == project.Id);
        Assert.Contains(roots, n => n.Kind == WorkItemKind.Task && n.Id == standalone.Id);
        Assert.DoesNotContain(roots, n => n.Kind == WorkItemKind.Task && n.ParentProjectId == project.Id);
    }

    [Fact]
    public void ListRootWorkItems_OrdersByDisplayName()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (workTree, tasks, projects) = CreateHarness(start);

        projects.Create("Zebra");
        projects.Create("Alpha");
        tasks.Create("beta task");

        var names = workTree.ListRootWorkItems().Select(n => n.DisplayName).ToList();

        Assert.Equal(["Alpha", "Zebra", "beta task"], names);
    }

    [Fact]
    public void GetChildren_ReturnsTasksForProject()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (workTree, tasks, projects) = CreateHarness(start);

        var project = projects.Create("Parent");
        var childA = tasks.Create("Authentication", project.Id);
        var childB = tasks.Create("UI Design", project.Id);
        tasks.Create("Standalone");

        var children = workTree.GetChildren(project.Id);

        Assert.Equal(2, children.Count);
        Assert.All(children, n => Assert.Equal(WorkItemKind.Task, n.Kind));
        Assert.All(children, n => Assert.Equal(project.Id, n.ParentProjectId));
        Assert.Equal(
            ["Authentication", "UI Design"],
            children.Select(n => n.DisplayName).ToList());
        Assert.DoesNotContain(children, n => n.Id == childA.Id && n.DisplayName != "Authentication");
        Assert.Contains(children, n => n.Id == childB.Id);
    }

    [Fact]
    public void GetChildren_ReturnsEmptyWhenProjectHasNoTasks()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (workTree, _, projects) = CreateHarness(start);

        var project = projects.Create("Empty");

        Assert.Empty(workTree.GetChildren(project.Id));
    }

    [Fact]
    public void WorkItemNode_ProjectHasNullParent()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (workTree, _, projects) = CreateHarness(start);

        var project = projects.Create("Root project");
        var node = workTree.ListRootWorkItems().Single(n => n.Id == project.Id);

        Assert.Equal(WorkItemKind.Project, node.Kind);
        Assert.Null(node.ParentProjectId);
    }
}
