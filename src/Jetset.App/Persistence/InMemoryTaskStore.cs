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

    public IReadOnlyList<WorkTask> ListByProject(Guid? projectId) =>
        _tasks.Values
            .Where(t => projectId is null ? t.ProjectId is null : t.ProjectId == projectId)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(Clone)
            .ToList();

    public IReadOnlyList<WorkTask> Search(string query) =>
        _tasks.Values
            .Where(t => MatchesSearchQuery(t, query))
            .OrderByDescending(t => t.UpdatedAt)
            .Select(Clone)
            .ToList();

    private static bool MatchesSearchQuery(WorkTask task, string query) =>
        task.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        ContainsField(task.Notes, query);

    private static bool ContainsField(string? value, string query) =>
        value is not null && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<WorkTask> ListByStatuses(IReadOnlyList<Models.TaskStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        if (statuses.Count == 0)
        {
            return [];
        }

        var set = statuses.ToHashSet();
        return _tasks.Values
            .Where(t => set.Contains(t.Status))
            .OrderByDescending(t => t.UpdatedAt)
            .Select(Clone)
            .ToList();
    }

    public WorkTask? GetRunningTask()
    {
        var running = _tasks.Values.Where(t => t.Status == Models.TaskStatus.Running).ToList();
        return running.Count switch
        {
            0 => null,
            1 => Clone(running[0]),
            _ => throw new InvalidOperationException("Multiple Running tasks found.")
        };
    }

    public int CountByProject(Guid projectId) =>
        _tasks.Values.Count(t => t.ProjectId == projectId);

    public void UnassignAllFromProject(Guid projectId)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var task in _tasks.Values.Where(t => t.ProjectId == projectId).ToList())
        {
            _tasks[task.Id] = Clone(new WorkTask
            {
                Id = task.Id,
                Title = task.Title,
                Status = task.Status,
                Origin = task.Origin,
                Notes = task.Notes,
                EstimateMinutes = task.EstimateMinutes,
                ProjectId = null,
                CreatedAt = task.CreatedAt,
                CompletedAt = task.CompletedAt,
                UpdatedAt = now,
                LastWorkedAt = task.LastWorkedAt
            });
        }
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
        Origin = t.Origin,
        Notes = t.Notes,
        EstimateMinutes = t.EstimateMinutes,
        ProjectId = t.ProjectId,
        CreatedAt = t.CreatedAt,
        CompletedAt = t.CompletedAt,
        UpdatedAt = t.UpdatedAt,
        LastWorkedAt = t.LastWorkedAt
    };
}
