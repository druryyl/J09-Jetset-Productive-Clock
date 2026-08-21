using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class AnalyticsServiceTests
{
    private static (
        AnalyticsService Analytics,
        SessionService Sessions,
        TaskService Tasks,
        ProjectService Projects,
        Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var sessionStore = new InMemorySessionStore();
        var taskStore = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => taskStore.List());
        var switchEventStore = new InMemoryTaskSwitchEventStore();
        var sessions = new SessionService(sessionStore, taskStore, switchEventStore, () => now);
        var tasks = new TaskService(taskStore, projectStore, () => now);
        var projects = new ProjectService(projectStore, taskStore, () => now);
        var analytics = new AnalyticsService(sessions, tasks, projects, switchEventStore, () => now);
        return (analytics, sessions, tasks, projects, value => now = value);
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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, _, _, _, _) = CreateHarness(start);

        Assert.Throws<ArgumentException>(() =>
            analytics.GetFocusTime(new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 21)));
    }

    [Fact]
    public void GetFocusTimeByTask_SumsAcrossSessions()
    {
        var start = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, _, _, _, _) = CreateHarness(start);

        Assert.Throws<ArgumentException>(() =>
            analytics.GetActivityHeatmap(new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 21)));
    }

    [Fact]
    public void GetFocusTimeByTask_ExcludesCancelledSessions()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, _, _, _, _) = CreateHarness(start);

        var streak = analytics.GetStreak();

        Assert.Equal(0, streak.CurrentStreak);
        Assert.Equal(0, streak.LongestStreak);
    }

    [Fact]
    public void GetStreak_CountsConsecutiveProductiveDays()
    {
        var start = new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

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
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(start);

        StartSession(sessions, tasks, start);
        setNow(start.AddSeconds(30));
        sessions.Finish();

        var streak = analytics.GetStreak();

        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(1, streak.LongestStreak);
    }

    [Fact]
    public void GetProjectMomentum_ThrowsWhenProjectNotFound()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, _, _, _, _) = CreateHarness(start);

        Assert.Throws<InvalidOperationException>(() =>
            analytics.GetProjectMomentum(
                Guid.NewGuid(),
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 22)));
    }

    [Fact]
    public void GetProjectMomentum_ThrowsWhenEndBeforeStart()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, _, _, projects, _) = CreateHarness(start);
        var project = projects.Create("Jetset");

        Assert.Throws<ArgumentException>(() =>
            analytics.GetProjectMomentum(
                project.Id,
                new DateOnly(2026, 8, 22),
                new DateOnly(2026, 8, 21)));
    }

    [Fact]
    public void GetProjectMomentum_AggregatesWeeklyFocusTime()
    {
        var start = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, projects, setNow) = CreateHarness(start);
        var project = projects.Create("Website");
        var task = tasks.Create("Homepage", project.Id);

        setNow(start);
        sessions.Start(task.Id, TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(60));
        sessions.Finish();

        setNow(start.AddDays(7));
        sessions.Start(task.Id, TimerMode.Stopwatch, null);
        setNow(start.AddDays(7).AddMinutes(30));
        sessions.Finish();

        setNow(start.AddDays(8));
        var momentum = analytics.GetProjectMomentum(
            project.Id,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 17));

        Assert.Equal(2, momentum.WeeklyFocusTrend.Count);
        Assert.Equal(TimeSpan.FromMinutes(60), momentum.WeeklyFocusTrend[0].FocusTime);
        Assert.Equal(TimeSpan.FromMinutes(30), momentum.WeeklyFocusTrend[1].FocusTime);
    }

    [Fact]
    public void GetProjectMomentum_CountsTasksCreatedAndCompletedPerWeek()
    {
        var start = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var (analytics, _, tasks, projects, setNow) = CreateHarness(start);
        var project = projects.Create("Website");

        setNow(start);
        var first = tasks.Create("First", project.Id);
        var second = tasks.Create("Second", project.Id);

        setNow(start.AddDays(1));
        tasks.TransitionStatus(first.Id, TaskStatus.Done);

        setNow(start.AddDays(7));
        var third = tasks.Create("Third", project.Id);
        setNow(start.AddDays(7));
        tasks.TransitionStatus(third.Id, TaskStatus.Done);

        var momentum = analytics.GetProjectMomentum(
            project.Id,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 17));

        Assert.Equal(3, momentum.TotalTasksCreated);
        Assert.Equal(2, momentum.TotalTasksCompleted);
        Assert.Equal(2, momentum.WeeklyCompletion[0].TasksCreated);
        Assert.Equal(1, momentum.WeeklyCompletion[0].TasksCompleted);
        Assert.Equal(1, momentum.WeeklyCompletion[1].TasksCreated);
        Assert.Equal(1, momentum.WeeklyCompletion[1].TasksCompleted);
    }

    [Fact]
    public void GetProjectMomentum_ExcludesTasksFromOtherProjects()
    {
        var start = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, projects, setNow) = CreateHarness(start);
        var project = projects.Create("Website");
        var otherProject = projects.Create("Other");
        var task = tasks.Create("Homepage", project.Id);
        var otherTask = tasks.Create("Other task", otherProject.Id);

        setNow(start);
        sessions.Start(task.Id, TimerMode.Stopwatch, null);
        setNow(start.AddMinutes(40));
        sessions.Finish();

        setNow(start.AddHours(1));
        sessions.Start(otherTask.Id, TimerMode.Stopwatch, null);
        setNow(start.AddHours(1).AddMinutes(20));
        sessions.Finish();

        var momentum = analytics.GetProjectMomentum(
            project.Id,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 16));

        Assert.Equal(TimeSpan.FromMinutes(40), momentum.WeeklyFocusTrend[0].FocusTime);
        Assert.Equal(1, momentum.TotalTasksCreated);
    }

    [Fact]
    public void GetSwitchMetrics_ThrowsWhenEndBeforeStart()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, _, _, _, _) = CreateHarness(start);

        Assert.Throws<ArgumentException>(() =>
            analytics.GetSwitchMetrics(new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 21)));
    }

    [Fact]
    public void GetSwitchMetrics_CountsSwitchesAcrossDays()
    {
        var day1 = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, _, setNow) = CreateHarness(day1);

        var first = tasks.Create("First");
        var second = tasks.Create("Second");

        setNow(day1);
        sessions.Start(first.Id, TimerMode.Stopwatch, null);
        setNow(day1.AddMinutes(5));
        sessions.Start(second.Id, TimerMode.Stopwatch, null);

        var day2 = new DateTimeOffset(2026, 8, 21, 14, 0, 0, TimeSpan.Zero);
        setNow(day2);
        sessions.SwitchTo(sessions.GetInProgressSessions().First(s => s.TaskId == first.Id).Id);
        setNow(day2.AddMinutes(5));
        sessions.SwitchTo(sessions.GetInProgressSessions().First(s => s.TaskId == second.Id).Id);

        var metrics = analytics.GetSwitchMetrics(
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 21));

        Assert.Equal(3, metrics.TotalSwitchCount);
        Assert.Equal(1.5, metrics.AveragePerDay);
        Assert.Equal(1, metrics.DailyCounts.First(d => d.Date == new DateOnly(2026, 8, 20)).SwitchCount);
        Assert.Equal(2, metrics.DailyCounts.First(d => d.Date == new DateOnly(2026, 8, 21)).SwitchCount);
        Assert.Equal(day2.ToLocalTime().Hour, metrics.BusiestHour);
    }

    [Fact]
    public void GetSwitchMetrics_ReturnsZeroWhenNoSwitches()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (analytics, sessions, tasks, _, _) = CreateHarness(start);

        var task = tasks.Create("Only task");
        sessions.Start(task.Id, TimerMode.Stopwatch, null);

        var metrics = analytics.GetSwitchMetrics(
            new DateOnly(2026, 8, 22),
            new DateOnly(2026, 8, 22));

        Assert.Equal(0, metrics.TotalSwitchCount);
        Assert.Equal(0, metrics.AveragePerDay);
        Assert.Null(metrics.BusiestHour);
        Assert.Single(metrics.DailyCounts);
        Assert.Equal(0, metrics.DailyCounts[0].SwitchCount);
    }
}
