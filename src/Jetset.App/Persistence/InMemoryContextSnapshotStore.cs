using Jetset.App.Models;

namespace Jetset.App.Persistence;

public sealed class InMemoryContextSnapshotStore : IContextSnapshotStore
{
    private readonly Dictionary<Guid, ContextSnapshot> _snapshots = new();

    public void Insert(ContextSnapshot snapshot) => _snapshots[snapshot.Id] = Clone(snapshot);

    public IReadOnlyList<ContextSnapshot> ListByTask(Guid taskId) =>
        _snapshots.Values
            .Where(s => s.TaskId == taskId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(Clone)
            .ToList();

    public ContextSnapshot? GetLatest(Guid taskId) =>
        _snapshots.Values
            .Where(s => s.TaskId == taskId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(Clone)
            .FirstOrDefault();

    public void DeleteByTask(Guid taskId)
    {
        foreach (var id in _snapshots.Values.Where(s => s.TaskId == taskId).Select(s => s.Id).ToList())
        {
            _snapshots.Remove(id);
        }
    }

    private static ContextSnapshot Clone(ContextSnapshot snapshot) =>
        new()
        {
            Id = snapshot.Id,
            TaskId = snapshot.TaskId,
            CreatedAt = snapshot.CreatedAt,
            CurrentStatus = snapshot.CurrentStatus,
            LastProgress = snapshot.LastProgress,
            NextAction = snapshot.NextAction,
            Blocker = snapshot.Blocker,
            Notes = snapshot.Notes
        };
}
