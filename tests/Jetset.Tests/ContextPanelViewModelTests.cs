using System.IO;
using System.Windows.Threading;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class ContextPanelViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static ContextPanelViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public ContextPanelViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetContextPanelTests", Guid.NewGuid().ToString("N"));
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
    public void ResolveFromProject_LoadsContextDeadlineAndRollup()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Jetset V2", new DateOnly(2026, 12, 31));
        services.Projects.UpdateContextText(project.Id, "Ship V2 workspace");

        var child = services.Tasks.Create("Auth", project.Id);
        child.EstimateMinutes = 120;
        services.Tasks.Update(child);

        using var vm = CreatePanel(services);
        var node = ProjectNode(project.Id, project.Name);

        vm.ResolveFromSelection(node);

        Assert.True(vm.IsVisible);
        Assert.Equal("Jetset V2", vm.ProjectName);
        Assert.Equal("Ship V2 workspace", vm.ContextText);
        Assert.True(vm.HasDeadline);
        Assert.Equal(new DateTime(2026, 12, 31), vm.DeadlineDate);
        Assert.Equal("0h", vm.SpentText);
        Assert.True(vm.HasEstimate);
        Assert.Equal("2h", vm.EstimateText);
        Assert.Equal("0h / 2h", vm.EffortSummaryText);
    }

    [Fact]
    public void ResolveFromTaskWithProject_ShowsOwningProjectContext()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Owning Project");
        services.Projects.UpdateContextText(project.Id, "Shared context");
        var task = services.Tasks.Create("Child task", project.Id);

        using var vm = CreatePanel(services);
        var node = TaskNode(task.Id, task.Title, project.Id);

        vm.ResolveFromSelection(node);

        Assert.True(vm.IsVisible);
        Assert.Equal("Owning Project", vm.ProjectName);
        Assert.Equal("Shared context", vm.ContextText);
        Assert.Equal(project.Id, vm.ResolvedProjectId);
    }

    [Fact]
    public void ResolveFromStandaloneTask_HidesPanel()
    {
        var services = CreateServices();
        var task = services.Tasks.CaptureToInbox("Standalone");

        using var vm = CreatePanel(services);
        vm.ResolveFromSelection(TaskNode(task.Id, task.Title, null));

        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void ContextTextEdit_PersistsAfterDebounce()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Persist test");

        using var vm = CreatePanel(services);
        vm.ResolveFromSelection(ProjectNode(project.Id, project.Name));
        vm.ContextText = "Persisted context";

        PumpDispatcher(TimeSpan.FromMilliseconds(900));

        Assert.Equal("Persisted context", services.Projects.GetContextText(project.Id));
    }

    [Fact]
    public void CanConvertToProject_WhenNonRunningTaskSelected()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Parent");
        var task = services.Tasks.Create("Convertible", project.Id);

        using var vm = CreatePanel(services);
        vm.ResolveFromSelection(TaskNode(task.Id, task.Title, project.Id));

        Assert.True(vm.CanConvertToProject);
        Assert.False(vm.CanConvertToTask);
    }

    [Fact]
    public void CanConvertToProject_FalseWhenRunningTaskSelected()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Parent");
        var task = services.Tasks.Create("Running", project.Id);
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        using var vm = CreatePanel(services);
        vm.ResolveFromSelection(TaskNode(task.Id, task.Title, project.Id));

        Assert.False(vm.CanConvertToProject);
    }

    [Fact]
    public void CanConvertToTask_WhenEmptyProjectSelected()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Empty");

        using var vm = CreatePanel(services);
        vm.ResolveFromSelection(ProjectNode(project.Id, project.Name));

        Assert.True(vm.CanConvertToTask);
        Assert.False(vm.CanConvertToProject);
    }

    [Fact]
    public void CanConvertToTask_FalseWhenProjectHasChildren()
    {
        var services = CreateServices();
        var project = services.Projects.Create("Parent");
        services.Tasks.Create("Child", project.Id);

        using var vm = CreatePanel(services);
        vm.ResolveFromSelection(ProjectNode(project.Id, project.Name));

        Assert.False(vm.CanConvertToTask);
    }

    private static ContextPanelViewModel CreatePanel(AppServices services)
    {
        return new ContextPanelViewModel(services, (_, _) => { });
    }

    private static WorkTreeNodeViewModel ProjectNode(Guid id, string name) =>
        new(
            WorkItemNode.FromProject(new Project { Id = id, Name = name }),
            false,
            false,
            string.Empty);

    private static WorkTreeNodeViewModel TaskNode(Guid id, string title, Guid? projectId) =>
        new(
            WorkItemNode.FromTask(new WorkTask { Id = id, Title = title, ProjectId = projectId }),
            false,
            false,
            string.Empty);

    private static void PumpDispatcher(TimeSpan wait)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = wait };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
