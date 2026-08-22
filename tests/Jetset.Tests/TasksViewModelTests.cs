using System.IO;
using System.Windows;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class TasksViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static TasksViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public TasksViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetTasksTests", Guid.NewGuid().ToString("N"));
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
    public void DefaultFilter_IsInbox()
    {
        var vm = new TasksViewModel(CreateServices());

        Assert.True(vm.IsInboxFilterSelected);
    }

    [Fact]
    public void SetStatusFilter_Ready_ShowsOnlyReadyTasks()
    {
        var services = CreateServices();
        var vm = new TasksViewModel(services);

        var inbox = services.Tasks.CaptureToInbox("Inbox item");
        var ready = services.Tasks.Create("Ready item");
        services.Tasks.ChangeStatus(ready.Id, TaskStatus.Ready);

        vm.SetStatusFilterCommand.Execute(StatusFilterMode.Ready);

        Assert.True(vm.IsReadyFilterSelected);
        Assert.Single(vm.Items);
        Assert.Equal(ready.Id, vm.Items[0].Id);
        Assert.NotEqual(inbox.Id, vm.Items[0].Id);
    }

    [Fact]
    public void StartWorkForTask_UsesTaskServiceStartTask()
    {
        var services = CreateServices();
        var vm = new TasksViewModel(services);

        var task = services.Tasks.Create("Do work");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);

        vm.SetStatusFilterCommand.Execute(StatusFilterMode.Ready);
        vm.StartWorkForTaskCommand.Execute(vm.Items[0]);

        Assert.Equal(TaskStatus.Running, services.Tasks.Get(task.Id)!.Status);
        Assert.Equal(task.Id, services.Sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void SwitchAndMarkWaiting_LeavesPreviousTaskWaiting()
    {
        var services = CreateServices();
        var vm = new TasksViewModel(services);

        var first = services.Tasks.Create("First");
        services.Tasks.ChangeStatus(first.Id, TaskStatus.Ready);
        var second = services.Tasks.Create("Second");
        services.Tasks.ChangeStatus(second.Id, TaskStatus.Ready);

        services.WorkExecution.StartWork(first.Id);
        vm.SetStatusFilterCommand.Execute(StatusFilterMode.Ready);
        vm.SwitchAndMarkWaitingForTaskCommand.Execute(vm.Items.Single(i => i.Id == second.Id));

        Assert.Equal(TaskStatus.Waiting, services.Tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(second.Id)!.Status);
    }

    [Fact]
    public void QuickCapture_CreatesInboxTaskWithoutDisturbingRunningWork()
    {
        var services = CreateServices();
        var vm = new TasksViewModel(services);

        var running = services.Tasks.Create("Running");
        services.Tasks.ChangeStatus(running.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(running.Id);

        vm.QuickAddTitle = "Captured thought";
        vm.AddTaskCommand.Execute(null);

        var inboxTask = services.Tasks.ListByStatuses([TaskStatus.Inbox]).Single();
        Assert.Equal("Captured thought", inboxTask.Title);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(running.Id)!.Status);
    }

    [Fact]
    public void ViewProjectContextRequested_NavigatesToProject()
    {
        var services = CreateServices();
        using var shell = new ShellViewModel(services);
        var vm = shell.Tasks;

        var project = services.Projects.Create("Docs");
        var task = services.Tasks.Create("Write guide", project.Id);
        vm.SetStatusFilterCommand.Execute(StatusFilterMode.All);
        vm.Selected = vm.Items.Single(i => i.Id == task.Id);

        vm.ViewProjectContextCommand.Execute(null);

        Assert.Equal(ShellArea.Projects, shell.CurrentArea);
        Assert.NotNull(shell.Projects.Selected);
        Assert.Equal(project.Id, shell.Projects.Selected!.Id);
    }

    [Fact]
    public void EditableStatusOptions_ExcludesRunningForNonRunningTask()
    {
        var services = CreateServices();
        var vm = new TasksViewModel(services);

        var task = services.Tasks.Create("Ready task");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        vm.SetStatusFilterCommand.Execute(StatusFilterMode.Ready);
        vm.Selected = vm.Items[0];

        Assert.DoesNotContain(TaskStatus.Running, vm.EditableStatusOptions);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
