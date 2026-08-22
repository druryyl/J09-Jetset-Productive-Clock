using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;

namespace Jetset.Tests;

public class AnalyticsServiceTests
{
    private static (
        AnalyticsService Analytics,
        SessionService Sessions,
        TaskService Tasks,
        Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var sessionStore = new InMemorySessionStore();
        var taskStore = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => taskStore.List());
        var sessions = new SessionService(sessionStore, taskStore, () => now);
        var tasks = new TaskService(taskStore, projectStore, () => now);
        var analytics = new AnalyticsService(sessions, tasks, () => now);
        return (analytics, sessions, tasks, value => now = value);
    }

    private static Guid StartSession(
        SessionService sessions,
        TaskService tasks,
        DateTimeOffset start,
        string title = "Task",
        Action<DateTimeOffset>? setNow = null)
    {
        var task = tasks.Create(title);
        sessions.Start(task.Id, TimerMode.Stopwatch, null);
        return task.Id;
    }

    [Fact]
    public void GetDailySummary_MatchesTodaysTotal()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(30));
        sessions.Finish();

        setNow(start.AddHours(1));
        StartSession(sessions, tasks, start, "Second");
        setNow(start.AddHours(1).AddMinutes(20));

        var reference = start.AddHours(1).AddMinutes(20);
        var summary = analytics.GetDailySummary(reference);
        var expected = sessions.GetTodaysTotal(reference);

        Assert.Equal(expected, summary.TotalFocusTime);
        Assert.Equal(2, summary.SessionCount);
        Assert.Equal(1, summary.CompletedSessionCount);
    }

    [Fact]
    public void GetDailySummary_ExcludesCancelledSessions()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(20));
        sessions.Finish();

        setNow(start.AddHours(1));
        StartSession(sessions, tasks, start, "Cancelled");
        setNow(start.AddHours(1).AddMinutes(10));
        sessions.Discard();

        var reference = start.AddHours(1).AddMinutes(10);
        var summary = analytics.GetDailySummary(reference);

        Assert.Equal(TimeSpan.FromMinutes(20), summary.TotalFocusTime);
        Assert.Single(summary.TaskBreakdown);
    }

    [Fact]
    public void GetDailySummary_GroupsByTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        var firstTaskId = StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(25));
        sessions.Finish();

        setNow(start.AddHours(1));
        sessions.Start(firstTaskId, TimerMode.Stopwatch, null);
        setNow(start.AddHours(1).AddMinutes(15));
        sessions.Finish();

        setNow(start.AddHours(2));
        StartSession(sessions, tasks, start, "Other task");
        setNow(start.AddHours(2).AddMinutes(10));
        sessions.Finish();

        var reference = start.AddHours(2).AddMinutes(10);
        var summary = analytics.GetDailySummary(reference);

        Assert.Equal(3, summary.SessionCount);
        Assert.Equal(2, summary.TaskBreakdown.Count);
        Assert.Equal(TimeSpan.FromMinutes(40), summary.TaskBreakdown.First(b => b.TaskId == firstTaskId).FocusTime);
        Assert.Equal(TimeSpan.FromMinutes(10), summary.TaskBreakdown.First(b => b.TaskTitle == "Other task").FocusTime);
    }

    [Fact]
    public void GetFocusTime_ReturnsDailyTotalsForRange()
    {
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(45));
        sessions.Finish();

        setNow(start.AddDays(1));
        StartSession(sessions, tasks, start.AddDays(1), "Day two");
        setNow(start.AddDays(1).AddMinutes(30));
        sessions.Finish();

        var results = analytics.GetFocusTime(
            DateOnly.FromDateTime(start.ToLocalTime().Date),
            DateOnly.FromDateTime(start.AddDays(1).ToLocalTime().Date));

        Assert.Equal(2, results.Count);
        Assert.Equal(TimeSpan.FromMinutes(45), results[0].FocusTime);
        Assert.Equal(TimeSpan.FromMinutes(30), results[1].FocusTime);
    }

    [Fact]
    public void GetFocusTime_ThrowsWhenEndBeforeStart()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, _, _, _) = CreateHarness(start);

        Assert.Throws<ArgumentException>(() =>
            analytics.GetFocusTime(new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 21)));
    }

    [Fact]
    public void GetFocusTimeByTask_SumsAcrossSessions()
    {
        var start = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        var taskId = StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(20));
        sessions.Finish();

        setNow(start.AddDays(1));
        sessions.Start(taskId, TimerMode.Stopwatch, null);
        setNow(start.AddDays(1).AddMinutes(35));
        sessions.Finish();

        setNow(start.AddDays(2));
        StartSession(sessions, tasks, start.AddDays(2), "Other");
        setNow(start.AddDays(2).AddMinutes(60));
        sessions.Finish();

        Assert.Equal(TimeSpan.FromMinutes(55), analytics.GetFocusTimeByTask(taskId));
    }

    [Fact]
    public void GetActivityHeatmap_ReturnsDailyFocusMinutes()
    {
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(45));
        sessions.Finish();

        setNow(start.AddDays(1));
        StartSession(sessions, tasks, start.AddDays(1), "Day two");
        setNow(start.AddDays(1).AddMinutes(30));
        sessions.Finish();

        setNow(start.AddDays(2));

        var heatmap = analytics.GetActivityHeatmap(
            DateOnly.FromDateTime(start.ToLocalTime().Date),
            DateOnly.FromDateTime(start.AddDays(2).ToLocalTime().Date));

        Assert.Equal(3, heatmap.Days.Count);
        Assert.Equal(45, heatmap.Days[0].FocusMinutes);
        Assert.Equal(30, heatmap.Days[1].FocusMinutes);
        Assert.Equal(0, heatmap.Days[2].FocusMinutes);
    }

    [Fact]
    public void GetActivityHeatmap_ThrowsWhenEndBeforeStart()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, _, _, _) = CreateHarness(start);

        Assert.Throws<ArgumentException>(() =>
            analytics.GetActivityHeatmap(new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 21)));
    }

    [Fact]
    public void GetFocusTimeByTask_ExcludesCancelledSessions()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        var taskId = StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(15));
        sessions.Finish();

        setNow(start.AddHours(1));
        sessions.Start(taskId, TimerMode.Stopwatch, null);
        setNow(start.AddHours(1).AddMinutes(10));
        sessions.Discard();

        Assert.Equal(TimeSpan.FromMinutes(15), analytics.GetFocusTimeByTask(taskId));
    }

    [Fact]
    public void GetStreak_ReturnsZeroWhenNoSessions()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, _, _, _) = CreateHarness(start);

        var streak = analytics.GetStreak();

        Assert.Equal(0, streak.CurrentStreak);
        Assert.Equal(0, streak.LongestStreak);
    }

    [Fact]
    public void GetStreak_CountsConsecutiveProductiveDays()
    {
        var start = new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        for (var offset = 0; offset < 3; offset++)
        {
            var day = start.AddDays(offset);
            setNow(day);
            StartSession(sessions, tasks, day, $"Day {offset}");
            setNow(day.AddMinutes(10));
            sessions.Finish();
        }

        setNow(start.AddDays(2).AddHours(12));
        var streak = analytics.GetStreak();

        Assert.Equal(3, streak.CurrentStreak);
        Assert.Equal(3, streak.LongestStreak);
    }

    [Fact]
    public void GetStreak_BreaksOnZeroDayGap()
    {
        var start = new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        setNow(start);
        StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(20));
        sessions.Finish();

        setNow(start.AddDays(2));
        StartSession(sessions, tasks, start.AddDays(2), "After gap");
        setNow(start.AddDays(2).AddMinutes(15));
        sessions.Finish();

        var streak = analytics.GetStreak();

        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(1, streak.LongestStreak);
    }

    [Fact]
    public void GetStreak_TracksLongestAcrossHistory()
    {
        var start = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        for (var offset = 0; offset < 4; offset++)
        {
            var day = start.AddDays(offset);
            setNow(day);
            StartSession(sessions, tasks, day);
            setNow(day.AddMinutes(10));
            sessions.Finish();
        }

        setNow(start.AddDays(6));
        StartSession(sessions, tasks, start.AddDays(6));
        setNow(start.AddDays(6).AddMinutes(10));
        sessions.Finish();

        setNow(start.AddDays(7));
        StartSession(sessions, tasks, start.AddDays(7));
        setNow(start.AddDays(7).AddMinutes(10));
        sessions.Finish();

        var streak = analytics.GetStreak();

        Assert.Equal(2, streak.CurrentStreak);
        Assert.Equal(4, streak.LongestStreak);
    }

    [Fact]
    public void GetStreak_PreservesStreakWhenTodayHasNoFocusYet()
    {
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        setNow(start);
        StartSession(sessions, tasks, start);
        setNow(start.AddMinutes(25));
        sessions.Finish();

        setNow(start.AddDays(1));
        StartSession(sessions, tasks, start.AddDays(1), "Yesterday");
        setNow(start.AddDays(1).AddMinutes(30));
        sessions.Finish();

        setNow(start.AddDays(2));
        var streak = analytics.GetStreak();

        Assert.Equal(2, streak.CurrentStreak);
        Assert.Equal(2, streak.LongestStreak);
    }

    [Fact]
    public void GetStreak_CountsAnyFocusTimeAsProductive()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, setNow) = CreateHarness(start);

        StartSession(sessions, tasks, start);
        setNow(start.AddSeconds(30));
        sessions.Finish();

        var streak = analytics.GetStreak();

        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(1, streak.LongestStreak);
    }
}
