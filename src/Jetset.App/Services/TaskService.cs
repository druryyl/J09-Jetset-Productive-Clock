using Jetset.App.Models;
using Jetset.App.Persistence;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.Services;

public sealed class TaskService
{
    private readonly ITaskStore _store;
    private readonly IProjectStore? _projectStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly HashSet<Guid> _resumedFromWaiting = [];

    public TaskService(ITaskStore store, Func<DateTimeOffset>? clock = null)
        : this(store, projectStore: null, clock)
    {
    }

    public TaskService(
        ITaskStore store,
        IProjectStore? projectStore,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _projectStore = projectStore;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public WorkTask Create(string title, Guid? projectId = null, TaskOrigin origin = TaskOrigin.Unplanned)
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

        return InsertTask(trimmed, TaskStatus.Inbox, origin, projectId);
    }

    public WorkTask CaptureToInbox(string title, Guid? projectId = null)
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

        return InsertTask(trimmed, TaskStatus.Inbox, TaskOrigin.Unplanned, projectId);
    }

    private WorkTask InsertTask(string title, TaskStatus status, TaskOrigin origin, Guid? projectId)
    {
        var now = _clock();
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Status = status,
            Origin = origin,
            ProjectId = projectId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _store.Insert(task);
        return task;
    }

    public WorkTask? Get(Guid id) => _store.Get(id);

    public WorkTask? GetRunningTask() => _store.GetRunningTask();

    public TaskWithContext? GetTaskWithContext(Guid id)
    {
        var task = _store.Get(id);
        if (task is null)
        {
            return null;
        }

        string? projectName = null;
        if (task.ProjectId is { } projectId)
        {
            projectName = _projectStore?.Get(projectId)?.Name;
        }

        return new TaskWithContext
        {
            Task = task,
            ProjectName = projectName
        };
    }

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

        var results = new Dictionary<Guid, WorkTask>();
        foreach (var task in _store.Search(trimmed))
        {
            results[task.Id] = task;
        }

        if (_projectStore is not null)
        {
            var matchingProjectIds = _projectStore.List()
                .Where(p => ContainsSearchTerm(p.ContextText, trimmed))
                .Select(p => p.Id)
                .ToHashSet();

            if (matchingProjectIds.Count > 0)
            {
                foreach (var task in _store.List())
                {
                    if (task.ProjectId is { } projectId && matchingProjectIds.Contains(projectId))
                    {
                        results[task.Id] = task;
                    }
                }
            }
        }

        return results.Values
            .OrderByDescending(t => t.UpdatedAt)
            .ToList();
    }

    public WorkTask AssignToProject(Guid taskId, Guid? projectId)
    {
        var existing = RequireTask(taskId);

        if (projectId is { } pid)
        {
            EnsureProjectExists(pid);
        }

        var updated = CopyTask(
            existing,
            projectId: projectId,
            updatedAt: _clock());

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

        var existing = RequireTask(task.Id);

        if (task.ProjectId is { } pid)
        {
            EnsureProjectExists(pid);
        }

        var updated = CopyTask(
            existing,
            title: trimmed,
            notes: NormalizeOptionalField(task.Notes),
            projectId: task.ProjectId,
            lastWorkedAt: task.LastWorkedAt,
            updatedAt: _clock());

        _store.Update(updated);
        return updated;
    }

    public WorkTask StartTask(Guid taskId, TaskStatus leavingStatus = TaskStatus.Ready)
    {
        if (leavingStatus is TaskStatus.Running or TaskStatus.Done or TaskStatus.Cancelled)
        {
            throw new ArgumentException(
                "Leaving status must be a non-terminal, non-running state.",
                nameof(leavingStatus));
        }

        var task = RequireTask(taskId);
        if (!TaskStatusRules.CanStart(task.Status))
        {
            throw new InvalidOperationException(
                $"Task \"{task.Title}\" cannot be started from status {task.Status}.");
        }

        var running = _store.GetRunningTask();
        if (running is not null && running.Id != taskId)
        {
            var resolvedLeavingStatus = ResolveLeavingStatus(running.Id, leavingStatus);
            PersistStatusChange(running, resolvedLeavingStatus);
        }

        if (task.Status == TaskStatus.Waiting)
        {
            _resumedFromWaiting.Add(taskId);
        }

        var now = _clock();
        var started = CopyTask(
            task,
            status: TaskStatus.Running,
            updatedAt: now,
            lastWorkedAt: now);
        _store.Update(started);
        return started;
    }

    public WorkTask StopTask(Guid taskId, TaskStatus targetStatus = TaskStatus.Ready)
    {
        if (targetStatus is TaskStatus.Running or TaskStatus.Done or TaskStatus.Cancelled)
        {
            throw new ArgumentException(
                "Stop target must be a non-terminal, non-running state.",
                nameof(targetStatus));
        }

        var task = RequireTask(taskId);
        if (task.Status != TaskStatus.Running)
        {
            throw new InvalidOperationException(
                $"Task \"{task.Title}\" is not Running.");
        }

        _resumedFromWaiting.Remove(taskId);
        return PersistStatusChange(task, targetStatus);
    }

    public WorkTask ChangeStatus(Guid taskId, TaskStatus newStatus)
    {
        var existing = RequireTask(taskId);

        if (newStatus == TaskStatus.Running)
        {
            return StartTask(taskId);
        }

        if (!TaskStatusRules.CanTransition(existing.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition task from {existing.Status} to {newStatus}.");
        }

        if (existing.Status == newStatus)
        {
            return existing;
        }

        if (existing.Status == TaskStatus.Running)
        {
            _resumedFromWaiting.Remove(taskId);
        }

        return PersistStatusChange(existing, newStatus);
    }

    public WorkTask TransitionStatus(Guid taskId, TaskStatus newStatus) =>
        ChangeStatus(taskId, newStatus);

    public WorkTask CompleteTask(Guid taskId) => ChangeStatus(taskId, TaskStatus.Done);

    public WorkTask RecordWorkStarted(Guid taskId)
    {
        var existing = RequireTask(taskId);
        var now = _clock();
        var updated = CopyTask(existing, updatedAt: now, lastWorkedAt: now);
        _store.Update(updated);
        return updated;
    }

    public IReadOnlyList<WorkTask> ListByStatuses(IReadOnlyList<TaskStatus> statuses) =>
        _store.ListByStatuses(statuses);

    public IReadOnlyList<WorkTask> ListActiveWork() =>
        _store.ListByStatuses([TaskStatus.Ready, TaskStatus.Waiting, TaskStatus.Inbox]);

    public bool IsEligibleForActiveWork(WorkTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return TaskStatusRules.IsEligibleForActiveWork(task.Status);
    }

    public void Delete(Guid id) => _store.Delete(id);

    private TaskStatus ResolveLeavingStatus(Guid leavingTaskId, TaskStatus leavingStatus)
    {
        if (leavingStatus == TaskStatus.Ready && _resumedFromWaiting.Remove(leavingTaskId))
        {
            return TaskStatus.Waiting;
        }

        return leavingStatus;
    }

    private WorkTask PersistStatusChange(WorkTask existing, TaskStatus newStatus)
    {
        var now = _clock();
        DateTimeOffset? completedAt = existing.CompletedAt;

        if (newStatus == TaskStatus.Done)
        {
            completedAt = now;
        }
        else if (existing.Status is TaskStatus.Done or TaskStatus.Cancelled)
        {
            completedAt = null;
        }

        var updated = new WorkTask
        {
            Id = existing.Id,
            Title = existing.Title,
            Status = newStatus,
            Origin = existing.Origin,
            Notes = existing.Notes,
            ProjectId = existing.ProjectId,
            CreatedAt = existing.CreatedAt,
            CompletedAt = completedAt,
            UpdatedAt = now,
            LastWorkedAt = existing.LastWorkedAt
        };

        _store.Update(updated);
        return updated;
    }

    private WorkTask RequireTask(Guid taskId) =>
        _store.Get(taskId)
        ?? throw new InvalidOperationException($"Task {taskId} was not found.");

    private static string? NormalizeOptionalField(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static WorkTask CopyTask(
        WorkTask source,
        string? title = null,
        TaskStatus? status = null,
        TaskOrigin? origin = null,
        string? notes = null,
        Guid? projectId = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? lastWorkedAt = null) =>
        new()
        {
            Id = source.Id,
            Title = title ?? source.Title,
            Status = status ?? source.Status,
            Origin = origin ?? source.Origin,
            Notes = notes ?? source.Notes,
            ProjectId = projectId ?? source.ProjectId,
            CreatedAt = createdAt ?? source.CreatedAt,
            CompletedAt = completedAt ?? source.CompletedAt,
            UpdatedAt = updatedAt ?? source.UpdatedAt,
            LastWorkedAt = lastWorkedAt ?? source.LastWorkedAt
        };

    private static bool ContainsSearchTerm(string? value, string query) =>
        value is not null && value.Contains(query, StringComparison.OrdinalIgnoreCase);

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
}
