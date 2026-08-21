using Jetset.App.Models;

namespace Jetset.App.Persistence;

public sealed class InMemoryTaskStore : ITaskStore
{
    private readonly Dictionary<Guid, WorkTask> _tasks = new();

    public WorkTask? Get(Guid id) =>
        _tasks.TryGetValue(id, out var task) ? Clone(task) : null;

    public IReadOnlyList<WorkTask> List() =>
        _tasks.Values
            .OrderByDescending(t => t.UpdatedAt)
            .Select(Clone)
            .ToList();

    public IReadOnlyList<WorkTask> Search(string query)
    {
        return _tasks.Values
            .Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.UpdatedAt)
            .Select(Clone)
            .ToList();
    }

    public void Insert(WorkTask task) => _tasks[task.Id] = Clone(task);

    public void Update(WorkTask task)
    {
        if (!_tasks.ContainsKey(task.Id))
        {
            throw new InvalidOperationException($"Task {task.Id} was not found.");
        }

        _tasks[task.Id] = Clone(task);
    }

    public void Delete(Guid id) => _tasks.Remove(id);

    private static WorkTask Clone(WorkTask t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Status = t.Status,
        Notes = t.Notes,
        CurrentStatus = t.CurrentStatus,
        LastProgress = t.LastProgress,
        NextAction = t.NextAction,
        Blocker = t.Blocker,
        ProjectId = t.ProjectId,
        MilestoneId = t.MilestoneId,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        LastWorkedAt = t.LastWorkedAt
    };
}
