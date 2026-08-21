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

    public IReadOnlyList<WorkTask> Search(string query)
    {
        return _tasks.Values
            .Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.UpdatedAt)
            .Select(Clone)
            .ToList();
    }

    public int CountByProject(Guid projectId) =>
        _tasks.Values.Count(t => t.ProjectId == projectId);

    public IReadOnlyList<WorkTask> ListByMilestone(Guid milestoneId) =>
        _tasks.Values
            .Where(t => t.MilestoneId == milestoneId)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(Clone)
            .ToList();

    public int CountByMilestone(Guid milestoneId) =>
        _tasks.Values.Count(t => t.MilestoneId == milestoneId);

    public int CountDoneByMilestone(Guid milestoneId) =>
        _tasks.Values.Count(t => t.MilestoneId == milestoneId && t.Status == Models.TaskStatus.Done);

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
                Notes = task.Notes,
                CurrentStatus = task.CurrentStatus,
                LastProgress = task.LastProgress,
                NextAction = task.NextAction,
                Blocker = task.Blocker,
                ProjectId = null,
                MilestoneId = null,
                CreatedAt = task.CreatedAt,
                UpdatedAt = now,
                LastWorkedAt = task.LastWorkedAt
            });
        }
    }

    public void UnassignAllFromMilestone(Guid milestoneId)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var task in _tasks.Values.Where(t => t.MilestoneId == milestoneId).ToList())
        {
            _tasks[task.Id] = Clone(new WorkTask
            {
                Id = task.Id,
                Title = task.Title,
                Status = task.Status,
                Notes = task.Notes,
                CurrentStatus = task.CurrentStatus,
                LastProgress = task.LastProgress,
                NextAction = task.NextAction,
                Blocker = task.Blocker,
                ProjectId = task.ProjectId,
                MilestoneId = null,
                CreatedAt = task.CreatedAt,
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
