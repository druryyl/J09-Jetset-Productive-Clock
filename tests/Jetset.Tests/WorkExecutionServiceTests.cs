using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class WorkExecutionServiceTests
{
    private static (
        WorkExecutionService Execution,
        SessionService Sessions,
        TaskService Tasks,
        InMemorySessionStore SessionStore,
        InMemoryTaskStore TaskStore,
        Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var sessionStore = new InMemorySessionStore();
        var taskStore = new InMemoryTaskStore();
        var sessions = new SessionService(sessionStore, taskStore, () => now);
        var tasks = new TaskService(taskStore, () => now);
        var execution = new WorkExecutionService(sessions, tasks);
        return (execution, sessions, tasks, sessionStore, taskStore, value => now = value);
    }

    [Fact]
    public void StartWork_SetsTaskRunningAndUpdatesLastWorkedAt()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Implement feature");

        setNow(start.AddMinutes(5));
        execution.StartWork(task.Id);

        var updated = tasks.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(TaskStatus.Running, updated!.Status);
        Assert.Equal(start.AddMinutes(5), updated.LastWorkedAt);
        Assert.Equal("Implement feature", execution.GetActiveTask()!.Title);
    }

    [Fact]
    public void StartWork_WhenPausedSessionExists_SwitchesInsteadOfCreatingNew()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, setNow) = CreateHarness(start);

        var first = tasks.Create("First task");
        var second = tasks.Create("Second task");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(10));
        execution.PauseWork();

        execution.StartWork(second.Id);
        setNow(start.AddMinutes(20));
        execution.PauseWork();

        var beforeCount = sessions.GetInProgressSessions().Count;
        execution.StartWork(first.Id);

        Assert.Equal(beforeCount, sessions.GetInProgressSessions().Count);
        Assert.Equal(first.Id, sessions.ActiveSession!.TaskId);
        Assert.Equal(TaskStatus.Running, tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Ready, tasks.Get(second.Id)!.Status);
    }

    [Fact]
    public void ResumeWork_WhenNoSessionExists_StartsStopwatch()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Quick resume");

        execution.ResumeWork(task.Id);

        Assert.NotNull(sessions.ActiveSession);
        Assert.Equal(task.Id, sessions.ActiveSession.TaskId);
        Assert.Equal(TimerMode.Stopwatch, sessions.ActiveSession.Mode);
        Assert.Equal(TaskStatus.Running, tasks.Get(task.Id)!.Status);
    }

    [Fact]
    public void ResumeWork_WhenPausedSessionExists_SwitchesToIt()
    {
        var start = new DateTimeOffset(2026, 8, 22, 11, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Paused task");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(15));
        execution.PauseWork();

        execution.StartWork(tasks.Create("Other").Id);
        execution.ResumeWork(task.Id);

        Assert.Equal(task.Id, sessions.ActiveSession!.TaskId);
        Assert.Equal(SessionState.Running, sessions.ActiveSession.State);
        Assert.Equal(TaskStatus.Running, tasks.Get(task.Id)!.Status);
    }

    [Fact]
    public void StartWork_WhenTaskIsDone_Throws()
    {
        var start = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Done task");
        tasks.TransitionStatus(task.Id, TaskStatus.Done);

        Assert.Throws<InvalidOperationException>(() => execution.StartWork(task.Id));
    }

    [Fact]
    public void SwitchToSession_UpdatesLastWorkedAtForTargetTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, setNow) = CreateHarness(start);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(5));
        execution.PauseWork();

        execution.StartWork(second.Id);
        setNow(start.AddMinutes(10));
        execution.PauseWork();

        var waiting = sessions.GetInProgressSessions().First(s => s.Id != sessions.ActiveSession!.Id);
        setNow(start.AddMinutes(20));
        execution.SwitchToSession(waiting.Id);

        var switchedTask = tasks.Get(waiting.TaskId);
        Assert.Equal(start.AddMinutes(20), switchedTask!.LastWorkedAt);
        Assert.Equal(waiting.TaskId, sessions.ActiveSession!.TaskId);
        Assert.Equal(TaskStatus.Running, switchedTask.Status);
    }

    [Fact]
    public void GetActiveTask_ReturnsNullWhenIdle()
    {
        var start = new DateTimeOffset(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
        var (execution, _, _, _, _, _) = CreateHarness(start);

        Assert.Null(execution.GetActiveTask());
    }

    [Fact]
    public void PauseWork_PausesRunningSessionButKeepsTaskRunning()
    {
        var start = new DateTimeOffset(2026, 8, 22, 15, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Feature work");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(12));

        execution.PauseWork();

        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);
        Assert.Equal(TaskStatus.Running, tasks.Get(task.Id)!.Status);
        Assert.Equal("Feature work", execution.GetActiveTask()!.Title);
    }

    [Fact]
    public void SwitchToSession_SwitchesWithoutContextSideEffects()
    {
        var start = new DateTimeOffset(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, setNow) = CreateHarness(start);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(8));
        execution.PauseWork();

        execution.StartWork(second.Id);
        var secondSessionId = sessions.ActiveSession!.Id;
        setNow(start.AddMinutes(15));
        execution.SwitchToSession(
            sessions.GetInProgressSessions().First(s => s.TaskId == first.Id).Id);

        Assert.Equal(first.Id, sessions.ActiveSession!.TaskId);
        Assert.Equal(secondSessionId, sessions.GetInProgressSessions().First(s => s.TaskId == second.Id).Id);
        Assert.Equal(TaskStatus.Running, tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Ready, tasks.Get(second.Id)!.Status);
    }

    [Fact]
    public void StartWork_WhenAnotherTaskIsRunning_SwitchesSessionAndLeavesPreviousReady()
    {
        var start = new DateTimeOffset(2026, 8, 22, 16, 30, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, _) = CreateHarness(start);

        var first = tasks.Create("Current");
        var second = tasks.Create("Next");
        execution.StartWork(first.Id);

        execution.StartWork(second.Id);

        Assert.Equal(second.Id, sessions.ActiveSession!.TaskId);
        Assert.Equal(2, sessions.GetInProgressSessions().Count);
        Assert.Equal(TaskStatus.Ready, tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, tasks.Get(second.Id)!.Status);
    }

    [Fact]
    public void StartWork_WhenAnotherTaskIsRunning_CanLeavePreviousAsWaiting()
    {
        var (execution, _, tasks, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        var first = tasks.Create("Current");
        var second = tasks.Create("Next");
        execution.StartWork(first.Id);

        execution.StartWork(second.Id, leavingStatus: TaskStatus.Waiting);

        Assert.Equal(TaskStatus.Waiting, tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, tasks.Get(second.Id)!.Status);
    }

    [Fact]
    public void StartWork_FromWaiting_PreservesWaitingWhenSwitchedWithDefaultLeavingStatus()
    {
        var (execution, _, tasks, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        var waiting = tasks.Create("Waiting task");
        var other = tasks.Create("Other task");
        var interrupt = tasks.Create("Interrupt");

        tasks.ChangeStatus(waiting.Id, TaskStatus.Waiting);
        execution.StartWork(waiting.Id);
        execution.StartWork(other.Id);
        execution.StartWork(interrupt.Id);

        Assert.Equal(TaskStatus.Waiting, tasks.Get(waiting.Id)!.Status);
        Assert.Equal(TaskStatus.Ready, tasks.Get(other.Id)!.Status);
        Assert.Equal(TaskStatus.Running, tasks.Get(interrupt.Id)!.Status);
    }

    [Fact]
    public void FinishWork_CompletesSessionAndStopsRunningTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Wrap up");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(25));

        var finished = execution.FinishWork("Shipped the slice");

        Assert.Equal(SessionState.Completed, finished.State);
        Assert.Equal("Shipped the slice", finished.Note);
        Assert.Null(sessions.ActiveSession);
        Assert.Equal(TaskStatus.Ready, tasks.Get(task.Id)!.Status);
        Assert.Null(tasks.GetRunningTask());
    }

    [Fact]
    public void FinishAtLastKnownActivity_CompletesSessionAndStopsRunningTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 18, 30, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Recovered");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(3));

        execution.FinishAtLastKnownActivity();

        Assert.Null(sessions.ActiveSession);
        Assert.Equal(TaskStatus.Ready, tasks.Get(task.Id)!.Status);
        Assert.Null(tasks.GetRunningTask());
    }

    [Fact]
    public void GetLeavingTask_ReturnsNullWhenPaused()
    {
        var start = new DateTimeOffset(2026, 8, 22, 19, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Paused");
        execution.StartWork(task.Id);
        execution.PauseWork();

        Assert.Null(execution.GetLeavingTask());
        Assert.Equal(task.Id, execution.GetActiveTask()!.Id);
    }

    [Fact]
    public void SwitchToSession_WithLeavingWaiting_MarksPreviousTaskWaiting()
    {
        var start = new DateTimeOffset(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, _) = CreateHarness(start);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");

        execution.StartWork(first.Id);
        execution.StartWork(second.Id);

        var firstSession = sessions.GetInProgressSessions().First(s => s.TaskId == first.Id);
        execution.SwitchToSession(firstSession.Id, leavingStatus: TaskStatus.Waiting);

        Assert.Equal(TaskStatus.Running, tasks.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Waiting, tasks.Get(second.Id)!.Status);
    }
}
