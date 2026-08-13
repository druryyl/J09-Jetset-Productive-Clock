using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;

namespace Jetset.Tests;

public class IdleAutoPauseControllerTests
{
    private sealed class FakeIdleService : ISystemIdleService
    {
        public TimeSpan IdleTime { get; set; }

        public TimeSpan GetIdleTime() => IdleTime;
    }

    private static (IdleAutoPauseController Controller, SessionService Sessions, FakeIdleService Idle, AppSettings Settings)
        CreateHarness(DateTimeOffset start, bool autoPause = true, int timeoutMinutes = 5, bool autoResume = true)
    {
        var now = start;
        var store = new InMemorySessionStore();
        var sessions = new SessionService(store, () => now);
        var idle = new FakeIdleService();
        var settings = new AppSettings
        {
            AutoPauseWhenIdle = autoPause,
            IdleTimeoutMinutes = timeoutMinutes,
            AutoResumeAfterIdle = autoResume
        };
        var controller = new IdleAutoPauseController(sessions, idle, () => settings);
        return (controller, sessions, idle, settings);
    }

    [Fact]
    public void GivenIdlePastTimeout_WhenEvaluate_ThenPausesOnce()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, idle, _) = CreateHarness(start);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        idle.IdleTime = TimeSpan.FromMinutes(5);

        controller.Evaluate();
        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);
        Assert.True(controller.PausedByIdle);

        controller.Evaluate();
        Assert.Equal(SessionState.Paused, sessions.ActiveSession.State);
        Assert.True(controller.PausedByIdle);
    }

    [Fact]
    public void GivenAlreadyIdlePaused_WhenStillIdle_ThenDoesNotThrowOrRepause()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, idle, _) = CreateHarness(start);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        idle.IdleTime = TimeSpan.FromMinutes(10);
        controller.Evaluate();

        var pauseCount = 0;
        sessions.SessionChanged += (_, _) => pauseCount++;
        controller.Evaluate();
        controller.Evaluate();

        Assert.Equal(0, pauseCount);
        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);
    }

    [Fact]
    public void GivenIdleCausedPause_WhenActivityReturnsWithAutoResume_ThenResumes()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, idle, _) = CreateHarness(start);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        idle.IdleTime = TimeSpan.FromMinutes(5);
        controller.Evaluate();
        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);

        idle.IdleTime = TimeSpan.FromSeconds(1);
        controller.Evaluate();

        Assert.Equal(SessionState.Running, sessions.ActiveSession!.State);
        Assert.False(controller.PausedByIdle);
    }

    [Fact]
    public void GivenManualPause_WhenActivityReturns_ThenDoesNotAutoResume()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, idle, _) = CreateHarness(start);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        controller.NotifyManualPause();
        sessions.Pause();

        idle.IdleTime = TimeSpan.FromSeconds(0);
        controller.Evaluate();

        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);
        Assert.False(controller.PausedByIdle);
    }

    [Fact]
    public void GivenFeatureDisabled_WhenIdle_ThenDoesNotPause()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, idle, _) = CreateHarness(start, autoPause: false);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        idle.IdleTime = TimeSpan.FromMinutes(30);
        controller.Evaluate();

        Assert.Equal(SessionState.Running, sessions.ActiveSession!.State);
        Assert.False(controller.PausedByIdle);
    }

    [Fact]
    public void GivenLockWhileRunning_WhenAutoPauseEnabled_ThenPausesAsIdle()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, _, _) = CreateHarness(start);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        controller.OnSessionLockedOrSuspended();

        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);
        Assert.True(controller.PausedByIdle);
    }

    [Fact]
    public void GivenIdlePause_WhenUnlockedWithAutoResume_ThenResumes()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, _, _) = CreateHarness(start);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        controller.OnSessionLockedOrSuspended();
        controller.OnSessionUnlockedOrResumed();

        Assert.Equal(SessionState.Running, sessions.ActiveSession!.State);
        Assert.False(controller.PausedByIdle);
    }

    [Fact]
    public void GivenIdlePause_WhenAutoResumeDisabled_ThenUnlockDoesNotResume()
    {
        var start = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var (controller, sessions, _, _) = CreateHarness(start, autoResume: false);

        sessions.Start("Work", TimerMode.Stopwatch, null);
        controller.OnSessionLockedOrSuspended();
        controller.OnSessionUnlockedOrResumed();

        Assert.Equal(SessionState.Paused, sessions.ActiveSession!.State);
        Assert.True(controller.PausedByIdle);
    }
}
