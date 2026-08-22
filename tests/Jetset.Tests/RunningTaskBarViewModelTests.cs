using System.IO;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class RunningTaskBarViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static RunningTaskBarViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public RunningTaskBarViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetRunningBarTests", Guid.NewGuid().ToString("N"));
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
    public void WhenIdle_ShowsNoRunningTask()
    {
        var services = CreateServices();
        using var bar = new RunningTaskBarViewModel(services);

        Assert.False(bar.HasRunningTask);
        Assert.Equal(string.Empty, bar.TaskTitle);
        Assert.Equal(string.Empty, bar.TimerDisplay);
    }

    [Fact]
    public void StartWork_ShowsRunningTaskAndStatus()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Authentication");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);

        using var bar = new RunningTaskBarViewModel(services);
        services.WorkExecution.StartWork(task.Id);

        Assert.True(bar.HasRunningTask);
        Assert.Equal("Authentication", bar.TaskTitle);
        Assert.Equal("Running", bar.StatusText);
        Assert.False(bar.IsPaused);
        Assert.False(string.IsNullOrEmpty(bar.TimerDisplay));
    }

    [Fact]
    public void PauseWork_UpdatesStatusAndEnablesResume()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Feature");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        using var bar = new RunningTaskBarViewModel(services);
        bar.PauseCommand.Execute(null);

        Assert.True(bar.HasRunningTask);
        Assert.True(bar.IsPaused);
        Assert.Equal("Paused", bar.StatusText);
        Assert.True(bar.ResumeCommand.CanExecute(null));
        Assert.False(bar.PauseCommand.CanExecute(null));
    }

    [Fact]
    public void ResumeWork_ReturnsToRunning()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Feature");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);
        services.WorkExecution.PauseWork();

        using var bar = new RunningTaskBarViewModel(services);
        bar.ResumeCommand.Execute(null);

        Assert.True(bar.HasRunningTask);
        Assert.False(bar.IsPaused);
        Assert.Equal("Running", bar.StatusText);
    }

    [Fact]
    public void MarkDone_CompletesTaskAndClearsBar()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Ship it");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        using var bar = new RunningTaskBarViewModel(services);
        bar.MarkDoneCommand.Execute(null);

        Assert.False(bar.HasRunningTask);
        Assert.Equal(TaskStatus.Done, services.Tasks.Get(task.Id)!.Status);
        Assert.Null(services.Sessions.ActiveSession);
    }

    [Fact]
    public void MarkWaiting_SetsTaskWaitingAndClearsBar()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Blocked");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        using var bar = new RunningTaskBarViewModel(services);
        bar.MarkWaitingCommand.Execute(null);

        Assert.False(bar.HasRunningTask);
        Assert.Equal(TaskStatus.Waiting, services.Tasks.Get(task.Id)!.Status);
        Assert.Null(services.Sessions.ActiveSession);
    }

    [Fact]
    public void StartSecondTask_LeavesFirstReadyAndShowsSecond()
    {
        var services = CreateServices();
        var first = services.Tasks.Create("Task A");
        var second = services.Tasks.Create("Task B");
        services.Tasks.ChangeStatus(first.Id, TaskStatus.Ready);
        services.Tasks.ChangeStatus(second.Id, TaskStatus.Ready);

        using var bar = new RunningTaskBarViewModel(services);
        services.WorkExecution.StartWork(first.Id);
        services.WorkExecution.StartWork(second.Id);

        Assert.Equal("Task B", bar.TaskTitle);
        Assert.Equal(TaskStatus.Ready, services.Tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(second.Id)!.Status);
        Assert.Equal(second.Id, services.Tasks.GetRunningTask()!.Id);
    }

    [Fact]
    public void TimerDisplay_ReflectsActiveSession()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Timed work");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        using var bar = new RunningTaskBarViewModel(services);

        Assert.NotNull(services.Sessions.ActiveSession);
        Assert.Matches(@"^\d{2}:\d{2}(:\d{2})?$", bar.TimerDisplay);
    }

    [Fact]
    public void WorkStateChanged_FiresWhenSessionChanges()
    {
        var services = CreateServices();
        var task = services.Tasks.Create("Notify");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);

        using var bar = new RunningTaskBarViewModel(services);
        var changeCount = 0;
        bar.WorkStateChanged += (_, _) => changeCount++;

        services.WorkExecution.StartWork(task.Id);

        Assert.True(changeCount >= 1);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
