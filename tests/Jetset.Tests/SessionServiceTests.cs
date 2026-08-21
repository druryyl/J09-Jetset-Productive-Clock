using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class SessionServiceTests
{
    private static (SessionService Service, InMemorySessionStore Store, InMemoryTaskStore TaskStore, Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var store = new InMemorySessionStore();
        var taskStore = new InMemoryTaskStore();
        var service = new SessionService(store, taskStore, null, () => now);
        return (service, store, taskStore, value => now = value);
    }

    private static Guid CreateTask(InMemoryTaskStore taskStore, string title, DateTimeOffset now)
    {
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Status = TaskStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        taskStore.Insert(task);
        return task.Id;
    }

    private static WorkSession StartSession(
        SessionService service,
        InMemoryTaskStore taskStore,
        DateTimeOffset now,
        string title,
        TimerMode mode,
        TimeSpan? duration)
    {
        var taskId = CreateTask(taskStore, title, now);
        return service.Start(taskId, mode, duration);
    }

    [Fact]
    public void GivenStopwatchWithPause_WhenFinished_ThenDurationExcludesPausedPeriods()
    {
        // Start 09:00, pause 09:30, resume 09:45, finish 10:15 => 1 hour
        var start = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var session = StartSession(service, taskStore, start, "API work", TimerMode.Stopwatch, null);

        setNow(start.AddMinutes(30));
        service.Pause();

        setNow(start.AddMinutes(45));
        service.Resume();

        setNow(start.AddMinutes(75));
        service.Finish();

        Assert.Equal(TimeSpan.FromHours(1), store.GetActiveDuration(session.Id, start.AddMinutes(75)));
    }

    [Fact]
    public void GivenMultiplePauseResumeIntervals_WhenSummed_ThenActiveDurationIsCorrect()
    {
        var start = new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var session = StartSession(service, taskStore, start, "Intervals", TimerMode.Stopwatch, null);

        setNow(start.AddMinutes(10));
        service.Pause();
        setNow(start.AddMinutes(15));
        service.Resume();
        setNow(start.AddMinutes(25));
        service.Pause();
        setNow(start.AddMinutes(40));
        service.Resume();
        setNow(start.AddMinutes(50));
        service.Finish();

        Assert.Equal(TimeSpan.FromMinutes(30), store.GetActiveDuration(session.Id));
        Assert.Equal(3, store.GetIntervals(session.Id).Count);
    }

    [Fact]
    public void GivenCountdown_WhenQueryingRemaining_ThenUsesTargetTime()
    {
        var start = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var (service, _, taskStore, setNow) = CreateHarness(start);

        var session = StartSession(service, taskStore, start, "Focus", TimerMode.Countdown, TimeSpan.FromMinutes(25));
        setNow(start.AddMinutes(10));

        // Reload session state from store via ActiveSession
        session = service.ActiveSession!;
        var remaining = SessionCalculations.GetCountdownRemaining(session, start.AddMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(15), remaining);
    }

    [Fact]
    public void GivenCountdownPastZero_WhenQuerying_ThenReportsOvertime()
    {
        var start = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var (service, _, taskStore, setNow) = CreateHarness(start);

        StartSession(service, taskStore, start, "Focus", TimerMode.Countdown, TimeSpan.FromMinutes(5));
        setNow(start.AddMinutes(12));
        var session = service.ActiveSession!;

        Assert.True(SessionCalculations.IsOvertime(session, start.AddMinutes(12)));
        Assert.Equal(TimeSpan.FromMinutes(7), SessionCalculations.GetOvertime(session, start.AddMinutes(12)));
        Assert.True(SessionCalculations.GetCountdownRemaining(session, start.AddMinutes(12)) < TimeSpan.Zero);
    }

    [Fact]
    public void GivenRunningSession_WhenStartingAnother_ThenFirstIsPausedAndSecondIsActive()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var first = StartSession(service, taskStore, start, "First", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(10));
        var second = StartSession(service, taskStore, start, "Second", TimerMode.Stopwatch, null);

        var inProgress = service.GetInProgressSessions();
        Assert.Equal(2, inProgress.Count);
        Assert.Equal(second.Id, service.ActiveSession!.Id);
        Assert.Equal(SessionState.Running, service.ActiveSession.State);
        Assert.Equal(SessionState.Paused, store.GetInProgressSessions().Single(s => s.Id == first.Id).State);
        Assert.Null(store.GetOpenInterval(first.Id));
        Assert.NotNull(store.GetOpenInterval(second.Id));
        Assert.Equal(TimeSpan.FromMinutes(10), store.GetActiveDuration(first.Id, start.AddMinutes(10)));
    }

    [Fact]
    public void GivenTwoInProgress_WhenSwitchToWaiting_ThenTimerMovesAndGapsExcluded()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var first = StartSession(service, taskStore, start, "Task A", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(10));
        var second = StartSession(service, taskStore, start, "Task B", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(25));

        service.SwitchTo(first.Id);
        setNow(start.AddMinutes(40));

        Assert.Equal(first.Id, service.ActiveSession!.Id);
        Assert.Equal(SessionState.Running, service.ActiveSession.State);
        Assert.Equal(SessionState.Paused, store.GetInProgressSessions().Single(s => s.Id == second.Id).State);
        Assert.Equal(TimeSpan.FromMinutes(25), store.GetActiveDuration(first.Id, start.AddMinutes(40)));
        Assert.Equal(TimeSpan.FromMinutes(15), store.GetActiveDuration(second.Id, start.AddMinutes(40)));
    }

    [Fact]
    public void GivenTwoInProgress_WhenFinishActive_ThenWaitingBecomesFocusedPaused()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, _, taskStore, setNow) = CreateHarness(start);

        var first = StartSession(service, taskStore, start, "Task A", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(5));
        StartSession(service, taskStore, start, "Task B", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(15));
        service.Finish();

        Assert.Equal(first.Id, service.ActiveSession!.Id);
        Assert.Equal(SessionState.Paused, service.ActiveSession.State);
        Assert.Equal("Task A", service.ActiveSession.TaskName);
        Assert.Single(service.GetInProgressSessions());
    }

    [Fact]
    public void GivenPausedAndRunning_WhenGetActiveSession_ThenPrefersRunning()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var first = StartSession(service, taskStore, start, "Older", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(5));
        service.Pause();
        setNow(start.AddMinutes(10));
        var second = StartSession(service, taskStore, start, "Newer running", TimerMode.Stopwatch, null);

        Assert.Equal(second.Id, store.GetActiveSession()!.Id);
        Assert.Equal(SessionState.Running, store.GetActiveSession()!.State);
        Assert.Equal(first.Id, store.GetInProgressSessions().Last().Id);
    }

    [Fact]
    public void GivenWaitingSession_WhenDeleted_ThenRejected()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, _, taskStore, setNow) = CreateHarness(start);

        var first = StartSession(service, taskStore, start, "Waiting", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(5));
        StartSession(service, taskStore, start, "Active", TimerMode.Stopwatch, null);

        Assert.Throws<InvalidOperationException>(() => service.DeleteSession(first.Id));
        Assert.Equal(2, service.GetInProgressSessions().Count);
    }

    [Fact]
    public void GivenRunningSession_WhenFinished_ThenOpenIntervalIsClosed()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var session = StartSession(service, taskStore, start, "Close interval", TimerMode.Stopwatch, null);
        Assert.NotNull(store.GetOpenInterval(session.Id));

        setNow(start.AddMinutes(20));
        service.Finish();

        Assert.Null(store.GetOpenInterval(session.Id));
        Assert.All(store.GetIntervals(session.Id), i => Assert.NotNull(i.EndedAt));
        Assert.Equal(SessionState.Completed, store.GetSessionsForLocalDay(start).Single().State);
    }

    [Fact]
    public void GivenPausedSession_WhenPausedAgain_ThenRejectedWithoutNewInterval()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var session = StartSession(service, taskStore, start, "Pause twice", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(5));
        service.Pause();
        var countAfterFirstPause = store.GetIntervals(session.Id).Count;

        Assert.Throws<InvalidOperationException>(() => service.Pause());
        Assert.Equal(countAfterFirstPause, store.GetIntervals(session.Id).Count);
        Assert.Null(store.GetOpenInterval(session.Id));
    }

    [Fact]
    public void GivenRunningSession_WhenResumed_ThenRejected()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, _, taskStore, _) = CreateHarness(start);

        StartSession(service, taskStore, start, "Already running", TimerMode.Stopwatch, null);

        Assert.Throws<InvalidOperationException>(() => service.Resume());
    }

    [Fact]
    public void GivenTodaysSessions_WhenTotalled_ThenCancelledExcludedAndActiveIncluded()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, _, taskStore, setNow) = CreateHarness(start);

        StartSession(service, taskStore, start, "Done", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(30));
        service.Finish();

        setNow(start.AddHours(1));
        StartSession(service, taskStore, start, "Discard me", TimerMode.Stopwatch, null);
        setNow(start.AddHours(1).AddMinutes(10));
        service.Discard();

        setNow(start.AddHours(2));
        StartSession(service, taskStore, start, "Still going", TimerMode.Stopwatch, null);
        setNow(start.AddHours(2).AddMinutes(15));

        var total = service.GetTodaysTotal(start.AddHours(2).AddMinutes(15));
        Assert.Equal(TimeSpan.FromMinutes(45), total);
    }

    [Fact]
    public void GivenUnfinishedSession_WhenAppRestarts_ThenRecoveryDetectsIt()
    {
        var start = new DateTimeOffset(2026, 8, 6, 9, 15, 0, TimeSpan.Zero);
        var store = new InMemorySessionStore();
        var taskStore = new InMemoryTaskStore();
        var now = start;
        var service = new SessionService(store, taskStore, null, () => now);

        StartSession(service, taskStore, start, "Implement API", TimerMode.Stopwatch, null);
        now = start.AddMinutes(20);
        service.Heartbeat();

        // Simulate restart with a new service over the same store
        var restarted = new SessionService(store, taskStore, null, () => now.AddMinutes(5));
        var unfinished = restarted.ActiveSession;

        Assert.NotNull(unfinished);
        Assert.Equal("Implement API", unfinished.TaskName);
        Assert.Equal(SessionState.Running, unfinished.State);
        Assert.NotNull(unfinished.LastHeartbeatAt);
        Assert.NotEqual(Guid.Empty, unfinished.TaskId);
    }

    [Fact]
    public void GivenTaskId_WhenSessionStarted_ThenSessionLinksToTask()
    {
        var start = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
        var (service, _, taskStore, _) = CreateHarness(start);

        var taskId = CreateTask(taskStore, "Linked task", start);
        var session = service.Start(taskId, TimerMode.Stopwatch, null);

        Assert.Equal(taskId, session.TaskId);
        Assert.Equal("Linked task", session.TaskName);
    }

    [Fact]
    public void GivenMissingTask_WhenStart_ThenRejected()
    {
        var start = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
        var (service, _, _, _) = CreateHarness(start);

        Assert.Throws<InvalidOperationException>(() =>
            service.Start(Guid.NewGuid(), TimerMode.Stopwatch, null));
    }

    [Fact]
    public void GivenCompletedSession_WhenDeleted_ThenRemovedFromStore()
    {
        var start = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, setNow) = CreateHarness(start);

        var session = StartSession(service, taskStore, start, "Ship feature", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(25));
        service.Finish();

        service.DeleteSession(session.Id);

        Assert.Empty(store.GetSessionsForLocalDay(start));
        Assert.Empty(store.GetIntervals(session.Id));
    }

    [Fact]
    public void GivenActiveSession_WhenDeleted_ThenRejectedAndSessionRemains()
    {
        var start = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
        var (service, store, taskStore, _) = CreateHarness(start);

        var session = StartSession(service, taskStore, start, "Still working", TimerMode.Stopwatch, null);

        Assert.Throws<InvalidOperationException>(() => service.DeleteSession(session.Id));
        Assert.Equal(session.Id, store.GetSessionsForLocalDay(start).Single().Id);
        Assert.NotNull(service.ActiveSession);
    }

    [Fact]
    public void SwitchTo_RecordsTaskSwitchEvent()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var switchEvents = new InMemoryTaskSwitchEventStore();
        var now = start;
        var store = new InMemorySessionStore();
        var taskStore = new InMemoryTaskStore();
        var service = new SessionService(store, taskStore, switchEvents, () => now);

        var firstTaskId = CreateTask(taskStore, "First", start);
        var secondTaskId = CreateTask(taskStore, "Second", start);

        service.Start(firstTaskId, TimerMode.Stopwatch, null);
        now = start.AddMinutes(5);
        service.Start(secondTaskId, TimerMode.Stopwatch, null);
        now = start.AddMinutes(12);
        service.SwitchTo(store.GetInProgressSessions().First(s => s.TaskId == firstTaskId).Id);

        var events = switchEvents.ListBetween(start, start.AddDays(1));
        Assert.Equal(2, events.Count);
        Assert.Equal(firstTaskId, events[0].FromTaskId);
        Assert.Equal(secondTaskId, events[0].ToTaskId);
        Assert.Equal(secondTaskId, events[1].FromTaskId);
        Assert.Equal(firstTaskId, events[1].ToTaskId);
    }
}

public class SessionCalculationsTests
{
    [Fact]
    public void GivenIntervals_WhenCalculating_ThenOpenIntervalUsesNow()
    {
        var start = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
        var intervals = new List<WorkInterval>
        {
            new()
            {
                Id = Guid.NewGuid(),
                WorkSessionId = Guid.NewGuid(),
                StartedAt = start,
                EndedAt = start.AddMinutes(10)
            },
            new()
            {
                Id = Guid.NewGuid(),
                WorkSessionId = Guid.NewGuid(),
                StartedAt = start.AddMinutes(20),
                EndedAt = null
            }
        };

        var duration = SessionCalculations.CalculateActiveDuration(intervals, start.AddMinutes(35));
        Assert.Equal(TimeSpan.FromMinutes(25), duration);
    }
}
