using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;

namespace Jetset.Tests;

public class ContextSnapshotServiceTests
{
    private static (
        ContextSnapshotService snapshots,
        TaskService tasks,
        InMemoryContextSnapshotStore snapshotStore,
        Action<DateTimeOffset> setNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var taskStore = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => taskStore.List());
        var milestoneStore = new InMemoryMilestoneStore();
        var snapshotStore = new InMemoryContextSnapshotStore();
        var tasks = new TaskService(taskStore, projectStore, milestoneStore, () => now);
        var snapshots = new ContextSnapshotService(snapshotStore, taskStore, () => now);
        return (snapshots, tasks, snapshotStore, value => now = value);
    }

    [Fact]
    public void Capture_PersistsAllContextFields()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (snapshots, tasks, store, setNow) = CreateHarness(start);
        var task = tasks.Create("Feature work");
        tasks.UpdateContext(
            task.Id,
            "In progress",
            "Finished design",
            "Implement store",
            "Waiting on API",
            "Session notes");
        setNow(start.AddMinutes(5));

        var captured = snapshots.Capture(task.Id);

        Assert.Equal(task.Id, captured.TaskId);
        Assert.Equal(start.AddMinutes(5), captured.CreatedAt);
        Assert.Equal("In progress", captured.CurrentStatus);
        Assert.Equal("Finished design", captured.LastProgress);
        Assert.Equal("Implement store", captured.NextAction);
        Assert.Equal("Waiting on API", captured.Blocker);
        Assert.Equal("Session notes", captured.Notes);

        var listed = store.ListByTask(task.Id);
        Assert.Single(listed);
        Assert.Equal(captured.Id, listed[0].Id);
    }

    [Fact]
    public void Capture_NormalizesWhitespaceToNull()
    {
        var (snapshots, tasks, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var task = tasks.Create("Empty context");
        tasks.UpdateContext(task.Id, "  ", null, "", "   ", "\t");

        var captured = snapshots.Capture(task.Id);

        Assert.Null(captured.CurrentStatus);
        Assert.Null(captured.LastProgress);
        Assert.Null(captured.NextAction);
        Assert.Null(captured.Blocker);
        Assert.Null(captured.Notes);
    }

    [Fact]
    public void Capture_WithMissingTask_Throws()
    {
        var (snapshots, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => snapshots.Capture(Guid.NewGuid()));
    }

    [Fact]
    public void ListByTask_ReturnsNewestFirst()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (snapshots, tasks, _, setNow) = CreateHarness(start);
        var task = tasks.Create("Ordered");
        tasks.UpdateContext(task.Id, "First", null, null, null, null);
        var first = snapshots.Capture(task.Id);

        setNow(start.AddMinutes(10));
        tasks.UpdateContext(task.Id, "Second", null, null, null, null);
        var second = snapshots.Capture(task.Id);

        var listed = snapshots.ListByTask(task.Id);

        Assert.Equal(2, listed.Count);
        Assert.Equal(second.Id, listed[0].Id);
        Assert.Equal(first.Id, listed[1].Id);
    }

    [Fact]
    public void GetLatest_ReturnsMostRecent()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (snapshots, tasks, _, setNow) = CreateHarness(start);
        var task = tasks.Create("Latest");
        tasks.UpdateContext(task.Id, "Older", null, null, null, null);
        snapshots.Capture(task.Id);

        setNow(start.AddHours(1));
        tasks.UpdateContext(task.Id, null, null, "Do the next thing", null, null);
        var latest = snapshots.Capture(task.Id);

        var result = snapshots.GetLatest(task.Id);

        Assert.NotNull(result);
        Assert.Equal(latest.Id, result.Id);
        Assert.Equal("Do the next thing", result.NextAction);
    }

    [Fact]
    public void GetLatest_WithNoSnapshots_ReturnsNull()
    {
        var (snapshots, tasks, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var task = tasks.Create("No snapshots");

        Assert.Null(snapshots.GetLatest(task.Id));
        Assert.Empty(snapshots.ListByTask(task.Id));
    }
}
