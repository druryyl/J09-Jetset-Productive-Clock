using System.IO;
using System.Reflection;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class ProjectsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static ProjectsViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public ProjectsViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetProjectsTests", Guid.NewGuid().ToString("N"));
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
    public void SelectProject_LoadsContextTextAndTasks()
    {
        var services = CreateServices();
        var vm = new ProjectsViewModel(services);

        var project = services.Projects.Create("Website");
        services.Projects.UpdateContextText(project.Id, "Resume with API notes.");
        services.Tasks.Create("Homepage", project.Id, TaskOrigin.Planned);

        vm.SelectProject(project.Id);

        Assert.NotNull(vm.Selected);
        Assert.Equal("Resume with API notes.", vm.Selected!.ContextText);
        Assert.Single(vm.ProjectTasks);
        Assert.Equal("Homepage", vm.ProjectTasks[0].Title);
    }

    [Fact]
    public void Save_PersistsContextText()
    {
        var services = CreateServices();
        var vm = new ProjectsViewModel(services);

        var project = services.Projects.Create("Docs");
        vm.SelectProject(project.Id);
        vm.Selected!.ContextText = "Updated context for the project.";
        vm.SaveCommand.Execute(null);

        Assert.Equal("Updated context for the project.", services.Projects.GetContextText(project.Id));
    }

    [Fact]
    public void StartWorkForTask_UsesWorkExecutionStartWork()
    {
        var services = CreateServices();
        var vm = new ProjectsViewModel(services);

        var project = services.Projects.Create("App");
        var task = services.Tasks.Create("Feature work", project.Id, TaskOrigin.Planned);
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);

        vm.SelectProject(project.Id);
        vm.StartWorkForTaskCommand.Execute(vm.ProjectTasks[0]);

        Assert.Equal(TaskStatus.Running, services.Tasks.Get(task.Id)!.Status);
        Assert.Equal(task.Id, services.Sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void SwitchAndMarkWaiting_LeavesPreviousTaskWaiting()
    {
        var services = CreateServices();
        var vm = new ProjectsViewModel(services);

        var project = services.Projects.Create("App");
        var first = services.Tasks.Create("First", project.Id, TaskOrigin.Planned);
        services.Tasks.ChangeStatus(first.Id, TaskStatus.Ready);
        var second = services.Tasks.Create("Second", project.Id, TaskOrigin.Planned);
        services.Tasks.ChangeStatus(second.Id, TaskStatus.Ready);

        services.WorkExecution.StartWork(first.Id);
        vm.SelectProject(project.Id);
        vm.SwitchAndMarkWaitingForTaskCommand.Execute(vm.ProjectTasks.Single(t => t.Id == second.Id));

        Assert.Equal(TaskStatus.Waiting, services.Tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(second.Id)!.Status);
    }

    [Fact]
    public void ViewModel_HasNoMomentumProperties()
    {
        var vm = new ProjectsViewModel(CreateServices());

        Assert.Null(vm.GetType().GetProperty("MomentumWeeks", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("MomentumRangeText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("MomentumSummaryText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(vm.GetType().GetProperty("HasMomentumWeeks", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void ViewModel_HasNoProjectAnalyticsProperties()
    {
        var vm = new ProjectsViewModel(CreateServices());
        var type = vm.GetType();

        Assert.Null(type.GetProperty("FocusTimeText", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetProperty("WeeklyFocusTrend", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetProperty("WeeklyCompletion", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetProperty("TotalTasksCreated", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetProperty("TotalTasksCompleted", BindingFlags.Instance | BindingFlags.Public));
        Assert.DoesNotContain(
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("Momentum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ViewModel_ExposesProjectContextAndTaskProperties()
    {
        var vm = new ProjectsViewModel(CreateServices());
        var type = vm.GetType();

        Assert.NotNull(type.GetProperty(nameof(ProjectsViewModel.ProjectTasks)));
        Assert.NotNull(type.GetProperty(nameof(ProjectsViewModel.HasProjectTasks)));
        Assert.NotNull(type.GetProperty(nameof(ProjectsViewModel.Selected)));
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
