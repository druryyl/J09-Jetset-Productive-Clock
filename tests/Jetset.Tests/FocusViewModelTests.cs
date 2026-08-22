using System.IO;
using System.Windows;
using Jetset.App.Models;
using Jetset.App.Services;
using Jetset.App.ViewModels;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class FocusViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static FocusViewModelTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public FocusViewModelTests()
    {
        WpfTestApplication.EnsureInitialized();
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetFocusTests", Guid.NewGuid().ToString("N"));
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
                // Best-effort cleanup of temp DB files.
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
            // Best-effort cleanup of temp directory.
        }
    }

    [Fact]
    public void StartWork_WhenTaskHasProject_DisplaysContextText()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var project = services.Projects.Create("Website redesign");
        services.Projects.UpdateContextText(project.Id, "Resume with API design notes.");
        var task = services.Tasks.Create("Implement checkout", project.Id);

        services.WorkExecution.StartWork(task.Id);

        Assert.True(focus.HasProjectContext);
        Assert.Equal("Website redesign", focus.ProjectName);
        Assert.Equal("Resume with API design notes.", focus.ProjectContextText);
    }

    [Fact]
    public void StartWork_WhenTaskHasNoProject_HidesProjectContext()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var task = services.Tasks.Create("Standalone task");
        services.WorkExecution.StartWork(task.Id);

        Assert.False(focus.HasProjectContext);
        Assert.Equal(string.Empty, focus.ProjectName);
        Assert.Equal(string.Empty, focus.ProjectContextText);
    }

    [Fact]
    public void FinishWork_ClearsProjectContext()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var project = services.Projects.Create("Docs");
        services.Projects.UpdateContextText(project.Id, "Outline the migration guide.");
        var task = services.Tasks.Create("Write guide", project.Id);

        services.WorkExecution.StartWork(task.Id);
        Assert.True(focus.HasProjectContext);

        services.WorkExecution.FinishWork();
        Assert.False(focus.HasProjectContext);
        Assert.Equal(string.Empty, focus.ProjectContextText);
    }

    [Fact]
    public void EditProjectContextRequested_RaisesEventWithProjectId()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var project = services.Projects.Create("Mobile app");
        services.Projects.UpdateContextText(project.Id, "Check auth flow.");
        var task = services.Tasks.Create("Fix login", project.Id);

        services.WorkExecution.StartWork(task.Id);

        Guid? raisedId = null;
        focus.EditProjectContextRequested += (_, id) => raisedId = id;
        focus.EditProjectContextCommand.Execute(null);

        Assert.Equal(project.Id, raisedId);
    }

    [Fact]
    public void RefreshTaskLists_PopulatesReadyAndWaitingSections()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var ready = services.Tasks.Create("Ready task");
        services.Tasks.ChangeStatus(ready.Id, TaskStatus.Ready);
        var waiting = services.Tasks.Create("Waiting task");
        services.Tasks.ChangeStatus(waiting.Id, TaskStatus.Waiting);
        services.Tasks.CaptureToInbox("Inbox only");

        focus.RefreshTaskLists();

        Assert.Single(focus.ReadyTasks);
        Assert.Equal(ready.Id, focus.ReadyTasks[0].Id);
        Assert.Single(focus.WaitingTasks);
        Assert.Equal(waiting.Id, focus.WaitingTasks[0].Id);
        Assert.True(focus.HasReadyTasks);
        Assert.True(focus.HasWaitingTasks);
    }

    [Fact]
    public void QuickCapture_DoesNotDisturbRunningTask()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var running = services.Tasks.Create("Current work");
        services.Tasks.ChangeStatus(running.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(running.Id);

        focus.QuickCaptureTitle = "Side note";
        focus.QuickCaptureCommand.Execute(null);

        var captured = services.Tasks.ListByStatuses([TaskStatus.Inbox]).Single();
        Assert.Equal("Side note", captured.Title);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(running.Id)!.Status);
        Assert.Equal(running.Id, services.Sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void StartTaskFromPicker_SwitchesWithDefaultReadyLeavingStatus()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var first = services.Tasks.Create("First");
        services.Tasks.ChangeStatus(first.Id, TaskStatus.Ready);
        var second = services.Tasks.Create("Second");
        services.Tasks.ChangeStatus(second.Id, TaskStatus.Ready);

        services.WorkExecution.StartWork(first.Id);
        focus.StartTaskCommand.Execute(focus.ReadyTasks.Single(t => t.Id == second.Id));

        Assert.Equal(TaskStatus.Ready, services.Tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(second.Id)!.Status);
        Assert.Equal(second.Id, services.Sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void SwitchAndMarkWaiting_LeavesPreviousTaskWaiting()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var first = services.Tasks.Create("Blocked work");
        services.Tasks.ChangeStatus(first.Id, TaskStatus.Ready);
        var second = services.Tasks.Create("Next up");
        services.Tasks.ChangeStatus(second.Id, TaskStatus.Ready);

        services.WorkExecution.StartWork(first.Id);
        focus.SwitchAndMarkWaitingCommand.Execute(focus.ReadyTasks.Single(t => t.Id == second.Id));

        Assert.Equal(TaskStatus.Waiting, services.Tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, services.Tasks.Get(second.Id)!.Status);
    }

    [Fact]
    public void CanSwitchTasks_IsFalseWhenIdle()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        Assert.True(focus.IsIdle);
        Assert.False(focus.CanSwitchTasks);
    }

    [Fact]
    public void CanSwitchTasks_IsTrueWhenSessionActive()
    {
        var services = CreateServices();
        using var focus = new FocusViewModel(services);

        var task = services.Tasks.Create("Active");
        services.Tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        services.WorkExecution.StartWork(task.Id);

        Assert.False(focus.IsIdle);
        Assert.True(focus.CanSwitchTasks);
    }

    private AppServices CreateServices()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new AppServices(path);
    }
}
