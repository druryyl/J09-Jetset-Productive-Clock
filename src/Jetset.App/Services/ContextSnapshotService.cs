using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

public sealed class ContextSnapshotService
{
    private readonly IContextSnapshotStore _store;
    private readonly ITaskStore _taskStore;
    private readonly Func<DateTimeOffset> _clock;

    public ContextSnapshotService(
        IContextSnapshotStore store,
        ITaskStore taskStore,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _taskStore = taskStore;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public ContextSnapshot Capture(Guid taskId)
    {
        var task = _taskStore.Get(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} was not found.");

        var snapshot = new ContextSnapshot
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            CreatedAt = _clock(),
            CurrentStatus = NormalizeContextField(task.CurrentStatus),
            LastProgress = NormalizeContextField(task.LastProgress),
            NextAction = NormalizeContextField(task.NextAction),
            Blocker = NormalizeContextField(task.Blocker),
            Notes = NormalizeContextField(task.Notes)
        };

        _store.Insert(snapshot);
        return snapshot;
    }

    public IReadOnlyList<ContextSnapshot> ListByTask(Guid taskId) =>
        _store.ListByTask(taskId);

    public ContextSnapshot? GetLatest(Guid taskId) =>
        _store.GetLatest(taskId);

    private static string? NormalizeContextField(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
