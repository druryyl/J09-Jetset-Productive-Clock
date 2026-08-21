using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class ResumeQueueServiceTests
{
    private static (
        ResumeQueueService Queue,
        WorkExecutionService Execution,
        SessionService Sessions,
        TaskService Tasks,
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
        var queue = new ResumeQueueService(tasks, sessions);
        return (queue, execution, sessions, tasks, value => now = value);
    }

    [Fact]
    public void GetOrderedTasks_ReturnsEmpty_WhenNoPausedSessions()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (queue, execution, _, tasks, _) = CreateHarness(start);

        var task = tasks.Create("Solo task");
        execution.StartWork(task.Id);

        Assert.Empty(queue.GetOrderedTasks());
    }

    [Fact]
    public void GetOrderedTasks_ExcludesFocusedSession()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (queue, execution, _, tasks, _) = CreateHarness(start);

        var task = tasks.Create("Focused task");
        execution.StartWork(task.Id);
        execution.PauseWork();

        Assert.Empty(queue.GetOrderedTasks());
    }

    [Fact]
    public void GetOrderedTasks_ListsOtherPausedSessions_WhenFocusedSessionExists()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (queue, execution, sessions, tasks, setNow) = CreateHarness(start);

        var first = tasks.Create("First task");
        var second = tasks.Create("Second task");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(5));
        execution.StartWork(second.Id);
        setNow(start.AddMinutes(10));
        sessions.Pause();

        var entries = queue.GetOrderedTasks();

        Assert.Single(entries);
        Assert.Equal(first.Id, entries[0].Task.Id);
        Assert.Equal(second.Id, sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void GetOrderedTasks_ExcludesDoneAndCancelledTasks()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (queue, execution, sessions, tasks, setNow) = CreateHarness(start);

        var active = tasks.Create("Active task");
        var done = tasks.Create("Done task");
        var cancelled = tasks.Create("Cancelled task");

        execution.StartWork(active.Id);
        setNow(start.AddMinutes(5));
        execution.StartWork(done.Id);
        setNow(start.AddMinutes(10));
        sessions.Pause();

        tasks.TransitionStatus(done.Id, TaskStatus.Done);
        tasks.TransitionStatus(cancelled.Id, TaskStatus.Cancelled);

        execution.StartWork(active.Id);

        Assert.Empty(queue.GetOrderedTasks());
    }

    [Fact]
    public void GetOrderedTasks_OrdersByLastWorkedAtDescending()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (queue, execution, sessions, tasks, setNow) = CreateHarness(start);

        var oldest = tasks.Create("Oldest");
        var middle = tasks.Create("Middle");
        var newest = tasks.Create("Newest");

        execution.StartWork(oldest.Id);
        setNow(start.AddMinutes(5));
        execution.StartWork(middle.Id);
        setNow(start.AddMinutes(10));
        execution.StartWork(newest.Id);
        setNow(start.AddMinutes(15));
        execution.StartWork(oldest.Id);

        var entries = queue.GetOrderedTasks();

        Assert.Equal(2, entries.Count);
        Assert.Equal(newest.Id, entries[0].Task.Id);
        Assert.Equal(middle.Id, entries[1].Task.Id);
        Assert.Equal(oldest.Id, sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void GetOrderedTasks_UpdatesOrderAfterPause()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (queue, execution, sessions, tasks, setNow) = CreateHarness(start);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(5));
        execution.StartWork(second.Id);
        setNow(start.AddMinutes(20));
        execution.PauseWork();

        var entries = queue.GetOrderedTasks();

        Assert.Single(entries);
        Assert.Equal(first.Id, entries[0].Task.Id);
        Assert.Equal(start.AddMinutes(20), tasks.Get(second.Id)!.LastWorkedAt);
        Assert.Equal(second.Id, sessions.ActiveSession!.TaskId);
    }

    [Fact]
    public void ResumeWork_FromQueueEntry_SwitchesToPausedSession()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (queue, execution, sessions, tasks, setNow) = CreateHarness(start);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");

        execution.StartWork(first.Id);
        setNow(start.AddMinutes(5));
        execution.StartWork(second.Id);
        setNow(start.AddMinutes(10));
        execution.StartWork(first.Id);

        var waitingTaskId = queue.GetOrderedTasks().Single().Task.Id;
        execution.ResumeWork(waitingTaskId);

        Assert.Equal(waitingTaskId, sessions.ActiveSession!.TaskId);
    }
}
