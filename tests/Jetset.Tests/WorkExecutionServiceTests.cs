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
        ContextSnapshotService Snapshots,
        InMemorySessionStore SessionStore,
        InMemoryTaskStore TaskStore,
        Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var sessionStore = new InMemorySessionStore();
        var taskStore = new InMemoryTaskStore();
        var snapshotStore = new InMemoryContextSnapshotStore();
        var sessions = new SessionService(sessionStore, taskStore, null, () => now);
        var tasks = new TaskService(taskStore, () => now);
        var snapshots = new ContextSnapshotService(snapshotStore, taskStore, () => now);
        var execution = new WorkExecutionService(sessions, tasks, snapshots);
        return (execution, sessions, tasks, snapshots, sessionStore, taskStore, value => now = value);
    }

    [Fact]
    public void StartWork_UpdatesLastWorkedAt()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, _, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Implement feature");

        setNow(start.AddMinutes(5));
        execution.StartWork(task.Id);

        var updated = tasks.Get(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(start.AddMinutes(5), updated!.LastWorkedAt);
        Assert.Equal("Implement feature", execution.GetActiveTask()!.Title);
    }

    [Fact]
    public void StartWork_WhenPausedSessionExists_SwitchesInsteadOfCreatingNew()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, _, setNow) = CreateHarness(start);

        var first = tasks.Create("First task");
        var second = tasks.Create("Second task");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(10));
        sessions.Pause();

        execution.StartWork(second.Id);
        setNow(start.AddMinutes(20));
        sessions.Pause();

        var beforeCount = sessions.GetInProgressSessions().Count;
        execution.StartWork(first.Id);

        Assert.Equal(beforeCount, sessions.GetInProgressSessions().Count);
        Assert.Equal(first.Id, sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void ResumeWork_WhenNoSessionExists_StartsStopwatch()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Quick resume");

        execution.ResumeWork(task.Id);

        Assert.NotNull(sessions.ActiveSession);
        Assert.Equal(task.Id, sessions.ActiveSession.TaskId);
        Assert.Equal(TimerMode.Stopwatch, sessions.ActiveSession.Mode);
    }

    [Fact]
    public void ResumeWork_WhenPausedSessionExists_SwitchesToIt()
    {
        var start = new DateTimeOffset(2026, 8, 22, 11, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Paused task");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(15));
        sessions.Pause();

        execution.StartWork(tasks.Create("Other").Id);
        execution.ResumeWork(task.Id);

        Assert.Equal(task.Id, sessions.ActiveSession!.TaskId);
        Assert.Equal(SessionState.Running, sessions.ActiveSession.State);
    }

    [Fact]
    public void StartWork_WhenTaskIsDone_Throws()
    {
        var start = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, _, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Done task");
        tasks.TransitionStatus(task.Id, TaskStatus.Done);

        Assert.Throws<InvalidOperationException>(() => execution.StartWork(task.Id));
    }

    [Fact]
    public void SwitchToSession_UpdatesLastWorkedAtForTargetTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, _, _, _, setNow) = CreateHarness(start);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(5));
        sessions.Pause();

        execution.StartWork(second.Id);
        setNow(start.AddMinutes(10));
        sessions.Pause();

        var waiting = sessions.GetInProgressSessions().First(s => s.Id != sessions.ActiveSession!.Id);
        setNow(start.AddMinutes(20));
        execution.SwitchToSession(waiting.Id);

        var switchedTask = tasks.Get(waiting.TaskId);
        Assert.Equal(start.AddMinutes(20), switchedTask!.LastWorkedAt);
        Assert.Equal(waiting.TaskId, sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void GetActiveTask_ReturnsNullWhenIdle()
    {
        var start = new DateTimeOffset(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
        var (execution, _, _, _, _, _, _) = CreateHarness(start);

        Assert.Null(execution.GetActiveTask());
    }

    [Fact]
    public void PauseWork_CapturesSnapshotFromCurrentContext()
    {
        var start = new DateTimeOffset(2026, 8, 22, 15, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, snapshots, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Feature work");
        tasks.UpdateContext(task.Id, "In progress", "Wrote tests", "Implement UI", null, "Keep notes");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(12));

        execution.PauseWork();

        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);
        var latest = snapshots.GetLatest(task.Id);
        Assert.NotNull(latest);
        Assert.Equal(start.AddMinutes(12), latest.CreatedAt);
        Assert.Equal("In progress", latest.CurrentStatus);
        Assert.Equal("Wrote tests", latest.LastProgress);
        Assert.Equal("Implement UI", latest.NextAction);
        Assert.Equal("Keep notes", latest.Notes);
    }

    [Fact]
    public void PauseWork_WithContextUpdate_WritesLiveContextThenSnapshots()
    {
        var start = new DateTimeOffset(2026, 8, 22, 15, 30, 0, TimeSpan.Zero);
        var (execution, _, tasks, snapshots, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Editable pause");
        execution.StartWork(task.Id);

        execution.PauseWork(new WorkingContext
        {
            CurrentStatus = "Blocked on review",
            LastProgress = "Opened PR",
            NextAction = "Address comments",
            Blocker = "Waiting on review",
            Notes = "Branch is feature/pr"
        });

        var updated = tasks.Get(task.Id)!;
        Assert.Equal("Blocked on review", updated.CurrentStatus);
        Assert.Equal("Opened PR", updated.LastProgress);
        Assert.Equal("Address comments", updated.NextAction);
        Assert.Equal("Waiting on review", updated.Blocker);
        Assert.Equal("Branch is feature/pr", updated.Notes);

        var latest = snapshots.GetLatest(task.Id)!;
        Assert.Equal("Opened PR", latest.LastProgress);
        Assert.Equal("Waiting on review", latest.Blocker);
    }

    [Fact]
    public void SwitchToSession_CapturesPriorTaskOnce()
    {
        var start = new DateTimeOffset(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, snapshots, _, _, setNow) = CreateHarness(start);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");
        tasks.UpdateContext(first.Id, "Working first", "Step A", "Step B", null, null);

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(8));
        execution.PauseWork();

        execution.StartWork(second.Id);
        var secondSessionId = sessions.ActiveSession!.Id;
        setNow(start.AddMinutes(15));
        execution.SwitchToSession(
            sessions.GetInProgressSessions().First(s => s.TaskId == first.Id).Id,
            new WorkingContext
            {
                CurrentStatus = "Parked second",
                LastProgress = "Drafted notes",
                NextAction = "Resume after first",
                Blocker = null,
                Notes = null
            });

        Assert.Equal(first.Id, sessions.ActiveSession!.TaskId);
        Assert.Single(snapshots.ListByTask(first.Id));
        Assert.Single(snapshots.ListByTask(second.Id));

        var secondTask = tasks.Get(second.Id)!;
        Assert.Equal("Parked second", secondTask.CurrentStatus);
        Assert.Equal("Drafted notes", secondTask.LastProgress);
        Assert.Equal(secondSessionId, sessions.GetInProgressSessions().First(s => s.TaskId == second.Id).Id);
    }

    [Fact]
    public void StartWork_WhenAnotherTaskIsRunning_CapturesLeavingTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 16, 30, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, snapshots, _, _, _) = CreateHarness(start);

        var first = tasks.Create("Current");
        var second = tasks.Create("Next");
        tasks.UpdateContext(first.Id, null, "Halfway", "Continue", null, null);
        execution.StartWork(first.Id);

        execution.StartWork(second.Id);

        Assert.Equal(second.Id, sessions.ActiveSession!.TaskId);
        var latest = snapshots.GetLatest(first.Id);
        Assert.NotNull(latest);
        Assert.Equal("Halfway", latest.LastProgress);
        Assert.Equal("Continue", latest.NextAction);
        Assert.Empty(snapshots.ListByTask(second.Id));
    }

    [Fact]
    public void StartWork_WhenIdle_DoesNotCaptureSnapshot()
    {
        var start = new DateTimeOffset(2026, 8, 22, 17, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, snapshots, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Fresh start");
        execution.StartWork(task.Id);

        Assert.Null(snapshots.GetLatest(task.Id));
        Assert.NotNull(execution.GetLeavingTask());
        Assert.Equal(task.Id, execution.GetLeavingTask()!.Id);
    }

    [Fact]
    public void FinishWork_UpdatesLastProgressAndCaptures()
    {
        var start = new DateTimeOffset(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);
        var (execution, sessions, tasks, snapshots, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Wrap up");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(25));

        var finished = execution.FinishWork(
            "Shipped the slice",
            new WorkingContext
            {
                CurrentStatus = "Ready for review",
                LastProgress = "Implemented S-11",
                NextAction = "Write follow-up notes",
                Blocker = null,
                Notes = "Keep the dialog skippable"
            });

        Assert.Equal(SessionState.Completed, finished.State);
        Assert.Equal("Shipped the slice", finished.Note);
        Assert.Null(sessions.ActiveSession);

        var updated = tasks.Get(task.Id)!;
        Assert.Equal("Implemented S-11", updated.LastProgress);
        Assert.Equal("Ready for review", updated.CurrentStatus);
        Assert.Equal("Write follow-up notes", updated.NextAction);

        var latest = snapshots.GetLatest(task.Id)!;
        Assert.Equal("Implemented S-11", latest.LastProgress);
        Assert.Equal("Keep the dialog skippable", latest.Notes);
    }

    [Fact]
    public void FinishWork_WithoutUpdate_StillCapturesCurrentContext()
    {
        var start = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, snapshots, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Skip finish");
        tasks.UpdateContext(task.Id, "Almost done", "Existing progress", null, null, null);
        execution.StartWork(task.Id);

        execution.FinishWork();

        var latest = snapshots.GetLatest(task.Id)!;
        Assert.Equal("Existing progress", latest.LastProgress);
        Assert.Equal("Almost done", latest.CurrentStatus);
        Assert.Equal("Existing progress", tasks.Get(task.Id)!.LastProgress);
    }

    [Fact]
    public void FinishAtLastKnownActivity_CapturesSnapshot()
    {
        var start = new DateTimeOffset(2026, 8, 22, 18, 30, 0, TimeSpan.Zero);
        var (execution, _, tasks, snapshots, _, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Recovered");
        execution.StartWork(task.Id);
        setNow(start.AddMinutes(3));

        execution.FinishAtLastKnownActivity();

        Assert.NotNull(snapshots.GetLatest(task.Id));
        Assert.Single(snapshots.ListByTask(task.Id));
    }

    [Fact]
    public void GetLeavingTask_ReturnsNullWhenPaused()
    {
        var start = new DateTimeOffset(2026, 8, 22, 19, 0, 0, TimeSpan.Zero);
        var (execution, _, tasks, _, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Paused");
        execution.StartWork(task.Id);
        execution.PauseWork();

        Assert.Null(execution.GetLeavingTask());
        Assert.Equal(task.Id, execution.GetActiveTask()!.Id);
    }
}
