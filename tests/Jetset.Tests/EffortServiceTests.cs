using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;

namespace Jetset.Tests;

public class EffortServiceTests
{
    private static (
        EffortService Effort,
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
        var sessions = new SessionService(sessionStore, taskStore, () => now);
        var tasks = new TaskService(taskStore, projectStore, () => now);
        var projects = new ProjectService(projectStore, taskStore, () => now);
        var effort = new EffortService(sessions, taskStore);
        return (effort, sessions, tasks, projects, value => now = value);
    }

    private static void AddSession(
        SessionService sessions,
        TaskService tasks,
        Guid taskId,
        DateTimeOffset start,
        TimeSpan duration,
        Action<DateTimeOffset> setNow)
    {
        sessions.Start(taskId, TimerMode.Stopwatch, null);
        setNow(start.Add(duration));
        sessions.Finish();
    }

    [Fact]
    public void GetTaskSpent_SumsNonCancelledSessions()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (effort, sessions, tasks, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Work");
        AddSession(sessions, tasks, task.Id, start, TimeSpan.FromMinutes(30), setNow);
        setNow(start.AddHours(1));
        AddSession(sessions, tasks, task.Id, start.AddHours(1), TimeSpan.FromMinutes(15), setNow);

        Assert.Equal(TimeSpan.FromMinutes(45), effort.GetTaskSpent(task.Id));
    }

    [Fact]
    public void GetTaskSpent_ExcludesCancelledSessions()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (effort, sessions, tasks, _, setNow) = CreateHarness(start);

        var task = tasks.Create("Work");
        AddSession(sessions, tasks, task.Id, start, TimeSpan.FromMinutes(20), setNow);

        setNow(start.AddHours(1));
        sessions.Start(task.Id, TimerMode.Stopwatch, null);
        setNow(start.AddHours(1).AddMinutes(10));
        sessions.Discard();

        Assert.Equal(TimeSpan.FromMinutes(20), effort.GetTaskSpent(task.Id));
    }

    [Fact]
    public void GetProjectRollup_SumsSpentAcrossChildTasks()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (effort, sessions, tasks, projects, setNow) = CreateHarness(start);

        var project = projects.Create("Jetset V2");
        var childA = tasks.Create("Auth", project.Id);
        var childB = tasks.Create("UI", project.Id);

        AddSession(sessions, tasks, childA.Id, start, TimeSpan.FromHours(2), setNow);
        setNow(start.AddHours(3));
        AddSession(sessions, tasks, childB.Id, start.AddHours(3), TimeSpan.FromHours(1), setNow);

        var rollup = effort.GetProjectRollup(project.Id);

        Assert.Equal(TimeSpan.FromHours(3), rollup.Spent);
    }

    [Fact]
    public void GetProjectRollup_SumsOnlyEstimatedTasks()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (effort, _, tasks, projects, _) = CreateHarness(start);

        var project = projects.Create("Mixed Estimates");
        var estimated = tasks.Create("Estimated", project.Id);
        var unestimated = tasks.Create("Unestimated", project.Id);

        estimated.EstimateMinutes = 120;
        tasks.Update(estimated);

        unestimated.EstimateMinutes = null;
        tasks.Update(unestimated);

        var rollup = effort.GetProjectRollup(project.Id);

        Assert.Equal(120, rollup.EstimateMinutes);
    }

    [Fact]
    public void GetProjectRollup_ReturnsNullEstimateWhenNoChildHasEstimate()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (effort, _, tasks, projects, _) = CreateHarness(start);

        var project = projects.Create("No Estimates");
        tasks.Create("Child A", project.Id);
        tasks.Create("Child B", project.Id);

        var rollup = effort.GetProjectRollup(project.Id);

        Assert.Null(rollup.EstimateMinutes);
        Assert.Equal(TimeSpan.Zero, rollup.Spent);
    }

    [Fact]
    public void GetProjectRollup_IncludesDoneAndCancelledTaskSpent()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (effort, sessions, tasks, projects, setNow) = CreateHarness(start);

        var project = projects.Create("Lifecycle");
        var doneTask = tasks.Create("Done work", project.Id);
        var cancelledTask = tasks.Create("Cancelled work", project.Id);

        AddSession(sessions, tasks, doneTask.Id, start, TimeSpan.FromMinutes(40), setNow);
        tasks.CompleteTask(doneTask.Id);

        setNow(start.AddHours(2));
        AddSession(sessions, tasks, cancelledTask.Id, start.AddHours(2), TimeSpan.FromMinutes(10), setNow);
        sessions.Start(cancelledTask.Id, TimerMode.Stopwatch, null);
        setNow(start.AddHours(3));
        sessions.Discard();

        var rollup = effort.GetProjectRollup(project.Id);

        Assert.Equal(TimeSpan.FromMinutes(50), rollup.Spent);
    }
}
