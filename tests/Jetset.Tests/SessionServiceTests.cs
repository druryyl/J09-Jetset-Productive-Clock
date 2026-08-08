using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;

namespace Jetset.Tests;

public class SessionServiceTests
{
    private static (SessionService Service, InMemorySessionStore Store, Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var store = new InMemorySessionStore();
        var service = new SessionService(store, () => now);
        return (service, store, value => now = value);
    }

    [Fact]
    public void GivenStopwatchWithPause_WhenFinished_ThenDurationExcludesPausedPeriods()
    {
        // Start 09:00, pause 09:30, resume 09:45, finish 10:15 => 1 hour
        var start = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
        var (service, store, setNow) = CreateHarness(start);

        var session = service.Start("API work", TimerMode.Stopwatch, null);

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
        var (service, store, setNow) = CreateHarness(start);

        var session = service.Start("Intervals", TimerMode.Stopwatch, null);

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
        var (service, _, setNow) = CreateHarness(start);

        var session = service.Start("Focus", TimerMode.Countdown, TimeSpan.FromMinutes(25));
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
        var (service, _, setNow) = CreateHarness(start);

        service.Start("Focus", TimerMode.Countdown, TimeSpan.FromMinutes(5));
        setNow(start.AddMinutes(12));
        var session = service.ActiveSession!;

        Assert.True(SessionCalculations.IsOvertime(session, start.AddMinutes(12)));
        Assert.Equal(TimeSpan.FromMinutes(7), SessionCalculations.GetOvertime(session, start.AddMinutes(12)));
        Assert.True(SessionCalculations.GetCountdownRemaining(session, start.AddMinutes(12)) < TimeSpan.Zero);
    }

    [Fact]
    public void GivenActiveSession_WhenStartingAnother_ThenRejected()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, _, _) = CreateHarness(start);

        service.Start("First", TimerMode.Stopwatch, null);

        Assert.Throws<InvalidOperationException>(() =>
            service.Start("Second", TimerMode.Stopwatch, null));
    }

    [Fact]
    public void GivenRunningSession_WhenFinished_ThenOpenIntervalIsClosed()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, store, setNow) = CreateHarness(start);

        var session = service.Start("Close interval", TimerMode.Stopwatch, null);
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
        var (service, store, setNow) = CreateHarness(start);

        var session = service.Start("Pause twice", TimerMode.Stopwatch, null);
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
        var (service, _, _) = CreateHarness(start);

        service.Start("Already running", TimerMode.Stopwatch, null);

        Assert.Throws<InvalidOperationException>(() => service.Resume());
    }

    [Fact]
    public void GivenTodaysSessions_WhenTotalled_ThenCancelledExcludedAndActiveIncluded()
    {
        var start = new DateTimeOffset(2026, 8, 6, 8, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);

        service.Start("Done", TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(30));
        service.Finish();

        setNow(start.AddHours(1));
        service.Start("Discard me", TimerMode.Stopwatch, null);
        setNow(start.AddHours(1).AddMinutes(10));
        service.Discard();

        setNow(start.AddHours(2));
        service.Start("Still going", TimerMode.Stopwatch, null);
        setNow(start.AddHours(2).AddMinutes(15));

        var total = service.GetTodaysTotal(start.AddHours(2).AddMinutes(15));
        Assert.Equal(TimeSpan.FromMinutes(45), total);
    }

    [Fact]
    public void GivenUnfinishedSession_WhenAppRestarts_ThenRecoveryDetectsIt()
    {
        var start = new DateTimeOffset(2026, 8, 6, 9, 15, 0, TimeSpan.Zero);
        var store = new InMemorySessionStore();
        var now = start;
        var service = new SessionService(store, () => now);

        service.Start("Implement API", TimerMode.Stopwatch, null);
        now = start.AddMinutes(20);
        service.Heartbeat();

        // Simulate restart with a new service over the same store
        var restarted = new SessionService(store, () => now.AddMinutes(5));
        var unfinished = restarted.ActiveSession;

        Assert.NotNull(unfinished);
        Assert.Equal("Implement API", unfinished.TaskName);
        Assert.Equal(SessionState.Running, unfinished.State);
        Assert.NotNull(unfinished.LastHeartbeatAt);
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
