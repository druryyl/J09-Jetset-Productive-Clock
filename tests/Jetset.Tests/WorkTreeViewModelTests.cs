using System.IO;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class WorkTreeViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static WorkTreeViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public WorkTreeViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetWorkTreeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var path in _dbPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void Load_ListsRootProjectsAndStandaloneTasks()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Jetset V2");
        services.Tasks.Create("Authentication", project.Id);
        services.Tasks.CaptureToInbox("Inbox item");

        using var vm = new WorkTreeViewModel(services);

        Assert.Equal(2, vm.RootNodes.Count);
        Assert.Contains(vm.RootNodes, n => n.Kind == WorkItemKind.Project && n.Title == "Jetset V2");
        Assert.Contains(vm.RootNodes, n => n.Kind == WorkItemKind.Task && n.Title == "Inbox item");
    }

    [Fact]
    public void Load_ExpandsProjectChildren()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Parent");
        services.Tasks.Create("Child task", project.Id);

        using var vm = new WorkTreeViewModel(services);
        var projectNode = vm.RootNodes.Single(n => n.Kind == WorkItemKind.Project);

        Assert.Single(projectNode.Children);
        Assert.Equal("Child task", projectNode.Children[0].Title);
    }

    [Fact]
    public void SelectingProject_ShowsContextPanel()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Context Project");

        using var vm = new WorkTreeViewModel(services);
        var projectNode = vm.RootNodes.Single();

        vm.SelectedNode = projectNode;

        Assert.True(vm.ContextPanel.IsVisible);
        Assert.Equal("Context Project", vm.ContextPanel.ProjectName);
    }

    [Fact]
    public void SelectingTaskWithProject_ShowsOwningProjectContext()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Owning Project");
        var task = services.Tasks.Create("Child", project.Id);

        using var vm = new WorkTreeViewModel(services);
        var projectNode = vm.RootNodes.Single(n => n.Kind == WorkItemKind.Project);
        var taskNode = projectNode.Children.Single();

        vm.SelectedNode = taskNode;

        Assert.True(vm.ContextPanel.IsVisible);
        Assert.Equal("Owning Project", vm.ContextPanel.ProjectName);
        Assert.Equal(project.Id, vm.ContextPanel.ResolvedProjectId);
        Assert.NotEqual(task.Id, vm.ContextPanel.ResolvedProjectId);
    }

    [Fact]
    public void SelectingStandaloneTask_HidesContextPanel()
    {
        var services = CreateServices();
        services.Tasks.CaptureToInbox("Standalone");

        using var vm = new WorkTreeViewModel(services);
        var taskNode = vm.RootNodes.Single();

        vm.SelectedNode = taskNode;

        Assert.False(vm.ContextPanel.IsVisible);
    }

    [Fact]
    public void ExpandState_PersistsAcrossReload()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Expandable");

        using (var vm = new WorkTreeViewModel(services))
        {
            var projectNode = vm.RootNodes.Single();
            projectNode.IsExpanded = true;
        }

        using var reloaded = new WorkTreeViewModel(services);
        var reloadedNode = reloaded.RootNodes.Single();

        Assert.True(reloadedNode.IsExpanded);
    }

    [Fact]
    public void RunningTask_IsMarkedOnNode()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Running task");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        using var vm = new WorkTreeViewModel(services);
        var runningNode = vm.RootNodes.Single(n => n.Kind == WorkItemKind.Task);

        Assert.True(runningNode.IsRunning);
    }

    [Fact]
    public void ReparentTask_AssignsTaskToProject()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Target");
        var task = services.Tasks.CaptureToInbox("Movable");

        using var vm = new WorkTreeViewModel(services);
        var success = vm.TryReparentTask(task.Id, project.Id, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(project.Id, services.Tasks.Get(task.Id)!.ProjectId);

        var projectNode = vm.RootNodes.Single(n => n.Kind == WorkItemKind.Project);
        Assert.True(projectNode.IsExpanded);
        Assert.Single(projectNode.Children);
        Assert.Equal("Movable", projectNode.Children[0].Title);
        Assert.DoesNotContain(vm.RootNodes, n => n.Kind == WorkItemKind.Task);
    }

    [Fact]
    public void ReparentTask_DetachesTaskToRoot()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Source");
        var task = services.Tasks.Create("Child", project.Id);

        using var vm = new WorkTreeViewModel(services);
        var success = vm.TryReparentTask(task.Id, null, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Null(services.Tasks.Get(task.Id)!.ProjectId);

        var projectNode = vm.RootNodes.Single(n => n.Kind == WorkItemKind.Project);
        Assert.Empty(projectNode.Children);
        Assert.Contains(vm.RootNodes, n => n.Kind == WorkItemKind.Task && n.Title == "Child");
    }

    [Fact]
    public void ReparentTask_RunningTask_KeepsRunning()
    {
        var services = CreateServices();
        var source = services.Projects.Create("Source");
        var target = services.Projects.Create("Target");
        var task = services.Tasks.Create("Running child", source.Id);
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        using var vm = new WorkTreeViewModel(services);
        var success = vm.TryReparentTask(task.Id, target.Id, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(task.Id)!.Status);
        Assert.Equal(target.Id, services.Tasks.Get(task.Id)!.ProjectId);
        Assert.Equal(task.Id, services.Tasks.GetRunningTask()!.Id);
        Assert.Equal(services.Sessions.ActiveSession!.TaskId, task.Id);
    }

    [Fact]
    public void Load_ShowsTaskEffortWhenEstimateSet()
    {
        var services = CreateServices();
        var task = services.Tasks.CaptureToInbox("Estimated task");
        services.Tasks.UpdateEstimate(task.Id, 720);

        using var vm = new WorkTreeViewModel(services);
        var taskNode = vm.RootNodes.Single();

        Assert.Equal("0h / 12h", taskNode.EffortDisplayText);
        Assert.True(taskNode.ShowEffort);
    }

    [Fact]
    public void Load_ShowsProjectRollupEffort()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Rollup Project");
        var child = services.Tasks.Create("Child", project.Id);
        services.Tasks.UpdateEstimate(child.Id, 120);

        using var vm = new WorkTreeViewModel(services);
        var projectNode = vm.RootNodes.Single(n => n.Kind == WorkItemKind.Project);

        Assert.Equal("0h / 2h", projectNode.EffortDisplayText);
    }

    [Fact]
    public void TryUpdateEstimate_PersistsAndRefreshesTree()
    {
        var services = CreateServices();
        var task = services.Tasks.CaptureToInbox("Editable");

        using var vm = new WorkTreeViewModel(services);
        var taskNode = vm.RootNodes.Single();

        var success = vm.TryUpdateEstimate(taskNode, "8", out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(480, services.Tasks.Get(task.Id)!.EstimateMinutes);
        Assert.Equal("0h / 8h", vm.RootNodes.Single().EffortDisplayText);
    }

    [Fact]
    public void QuickCapture_CreatesInboxTaskAtRoot()
    {
        var services = CreateServices();
        services.Projects.Create("Existing project");

        using var vm = new WorkTreeViewModel(services);
        vm.QuickCaptureTitle = "New idea";
        vm.QuickCaptureCommand.Execute(null);

        var captured = services.Tasks.ListByStatuses([TaskStatus.Inbox]).Single();
        Assert.Equal("New idea", captured.Title);
        Assert.Null(captured.ProjectId);
        Assert.Equal(string.Empty, vm.QuickCaptureTitle);
        Assert.Contains(vm.RootNodes, n => n.Kind == WorkItemKind.Task && n.Title == "New idea");
    }

    [Fact]
    public void QuickCapture_DoesNotDisturbRunningTask()
    {
        var services = CreateServices();
        using var vm = new WorkTreeViewModel(services);

        var running = services.Tasks.Create("Current work");
        services.Tasks.ChangeStatus(running.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(running.Id);

        vm.QuickCaptureTitle = "Side note";
        vm.QuickCaptureCommand.Execute(null);

        var captured = services.Tasks.ListByStatuses([TaskStatus.Inbox]).Single();
        Assert.Equal("Side note", captured.Title);
        Assert.Null(captured.ProjectId);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(running.Id)!.Status);
        Assert.Equal(running.Id, services.Sessions.ActiveSession!.TaskId);
        Assert.Equal(running.Id, services.Tasks.GetRunningTask()!.Id);
    }

    [Fact]
    public void QuickCapture_WithBlankTitle_DoesNotExecute()
    {
        var services = CreateServices();

        using var vm = new WorkTreeViewModel(services);
        vm.QuickCaptureTitle = "   ";

        Assert.False(vm.QuickCaptureCommand.CanExecute(null));
    }

    [Fact]
    public void StartTask_FromWorkTree_StartsRunningTask()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Startable");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);

        using var vm = new WorkTreeViewModel(services);
        var taskNode = vm.RootNodes.Single();

        vm.StartTaskCommand.Execute(taskNode);

        Assert.Equal(TaskStatus.Running, services.Tasks.Get(task.Id)!.Status);
        Assert.True(vm.RunningTaskBar.HasRunningTask);
        Assert.Equal("Startable", vm.RunningTaskBar.TaskTitle);
        Assert.True(taskNode.IsRunning || vm.RootNodes.Single().IsRunning);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
