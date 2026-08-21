using Jetset.App.Models;
using Jetset.App.Persistence;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.Services;

public sealed class TaskService
{
    private readonly ITaskStore _store;
    private readonly IProjectStore? _projectStore;
    private readonly IMilestoneStore? _milestoneStore;
    private readonly Func<DateTimeOffset> _clock;

    public TaskService(ITaskStore store, Func<DateTimeOffset>? clock = null)
        : this(store, projectStore: null, milestoneStore: null, clock)
    {
    }

    public TaskService(ITaskStore store, IProjectStore? projectStore, Func<DateTimeOffset>? clock = null)
        : this(store, projectStore, milestoneStore: null, clock)
    {
    }

    public TaskService(
        ITaskStore store,
        IProjectStore? projectStore,
        IMilestoneStore? milestoneStore,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _projectStore = projectStore;
        _milestoneStore = milestoneStore;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public WorkTask Create(string title, Guid? projectId = null)
    {
        var trimmed = title.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Task title is required.", nameof(title));
        }

        if (projectId is { } pid)
        {
            EnsureProjectExists(pid);
        }

        var now = _clock();
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            Title = trimmed,
            Status = TaskStatus.Active,
            ProjectId = projectId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _store.Insert(task);
        return task;
    }

    public WorkTask? Get(Guid id) => _store.Get(id);

    public IReadOnlyList<WorkTask> List() => _store.List();

    public IReadOnlyList<WorkTask> ListByProject(Guid? projectId) =>
        _store.ListByProject(projectId);

    public IReadOnlyList<WorkTask> Search(string query)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        return _store.Search(trimmed);
    }

    public WorkTask AssignToProject(Guid taskId, Guid? projectId)
    {
        var existing = _store.Get(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} was not found.");

        if (projectId is { } pid)
        {
            EnsureProjectExists(pid);
        }

        var projectChanged = existing.ProjectId != projectId;
        var updated = new WorkTask
        {
            Id = existing.Id,
            Title = existing.Title,
            Status = existing.Status,
            Notes = existing.Notes,
            CurrentStatus = existing.CurrentStatus,
            LastProgress = existing.LastProgress,
            NextAction = existing.NextAction,
            Blocker = existing.Blocker,
            ProjectId = projectId,
            MilestoneId = projectChanged ? null : existing.MilestoneId,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = _clock(),
            LastWorkedAt = existing.LastWorkedAt
        };

        _store.Update(updated);
        return updated;
    }

    public WorkTask AssignToMilestone(Guid taskId, Guid? milestoneId)
    {
        var existing = _store.Get(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} was not found.");

        if (milestoneId is { } mid)
        {
            if (existing.ProjectId is null)
            {
                throw new InvalidOperationException(
                    "Task must belong to a project before assigning a milestone.");
            }

            EnsureMilestoneBelongsToProject(mid, existing.ProjectId.Value);
        }

        var updated = new WorkTask
        {
            Id = existing.Id,
            Title = existing.Title,
            Status = existing.Status,
            Notes = existing.Notes,
            CurrentStatus = existing.CurrentStatus,
            LastProgress = existing.LastProgress,
            NextAction = existing.NextAction,
            Blocker = existing.Blocker,
            ProjectId = existing.ProjectId,
            MilestoneId = milestoneId,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = _clock(),
            LastWorkedAt = existing.LastWorkedAt
        };

        _store.Update(updated);
        return updated;
    }

    public WorkTask Update(WorkTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var trimmed = task.Title.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Task title is required.", nameof(task));
        }

        var existing = _store.Get(task.Id)
            ?? throw new InvalidOperationException($"Task {task.Id} was not found.");

        if (task.ProjectId is { } pid)
        {
            EnsureProjectExists(pid);
        }

        var projectChanged = existing.ProjectId != task.ProjectId;
        var milestoneId = projectChanged ? null : task.MilestoneId;

        if (milestoneId is { } mid)
        {
            if (task.ProjectId is null)
            {
                throw new InvalidOperationException(
                    "Task must belong to a project before assigning a milestone.");
            }

            EnsureMilestoneBelongsToProject(mid, task.ProjectId.Value);
        }

        var updated = new WorkTask
        {
            Id = existing.Id,
            Title = trimmed,
            Status = existing.Status,
            Notes = string.IsNullOrWhiteSpace(task.Notes) ? null : task.Notes.Trim(),
            CurrentStatus = task.CurrentStatus,
            LastProgress = task.LastProgress,
            NextAction = task.NextAction,
            Blocker = task.Blocker,
            ProjectId = task.ProjectId,
            MilestoneId = milestoneId,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = _clock(),
            LastWorkedAt = task.LastWorkedAt
        };

        _store.Update(updated);
        return updated;
    }

    public WorkTask TransitionStatus(Guid taskId, TaskStatus newStatus)
    {
        var existing = _store.Get(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} was not found.");

        if (!TaskStatusRules.CanTransition(existing.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition task from {existing.Status} to {newStatus}.");
        }

        if (existing.Status == newStatus)
        {
            return existing;
        }

        var updated = new WorkTask
        {
            Id = existing.Id,
            Title = existing.Title,
            Status = newStatus,
            Notes = existing.Notes,
            CurrentStatus = existing.CurrentStatus,
            LastProgress = existing.LastProgress,
            NextAction = existing.NextAction,
            Blocker = existing.Blocker,
            ProjectId = existing.ProjectId,
            MilestoneId = existing.MilestoneId,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = _clock(),
            LastWorkedAt = existing.LastWorkedAt
        };

        _store.Update(updated);
        return updated;
    }

    public IReadOnlyList<WorkTask> ListActiveWork() =>
        _store.ListByStatuses([TaskStatus.Active, TaskStatus.Blocked]);

    public bool IsEligibleForActiveWork(WorkTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return TaskStatusRules.IsEligibleForActiveWork(task.Status);
    }

    public void Delete(Guid id) => _store.Delete(id);

    private void EnsureProjectExists(Guid projectId)
    {
        if (_projectStore is null)
        {
            return;
        }

        if (_projectStore.Get(projectId) is null)
        {
            throw new InvalidOperationException($"Project {projectId} was not found.");
        }
    }

    private void EnsureMilestoneBelongsToProject(Guid milestoneId, Guid projectId)
    {
        if (_milestoneStore is null)
        {
            return;
        }

        var milestone = _milestoneStore.Get(milestoneId)
            ?? throw new InvalidOperationException($"Milestone {milestoneId} was not found.");

        if (milestone.ProjectId != projectId)
        {
            throw new InvalidOperationException(
                $"Milestone {milestoneId} does not belong to project {projectId}.");
        }
    }
}
