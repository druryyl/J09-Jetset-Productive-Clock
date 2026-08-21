using Jetset.App.Models;
using Jetset.App.Persistence;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.Services;

public sealed class TaskService
{
    private readonly ITaskStore _store;
    private readonly Func<DateTimeOffset> _clock;

    public TaskService(ITaskStore store, Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public WorkTask Create(string title)
    {
        var trimmed = title.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Task title is required.", nameof(title));
        }

        var now = _clock();
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            Title = trimmed,
            Status = TaskStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _store.Insert(task);
        return task;
    }

    public WorkTask? Get(Guid id) => _store.Get(id);

    public IReadOnlyList<WorkTask> List() => _store.List();

    public IReadOnlyList<WorkTask> Search(string query)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        return _store.Search(trimmed);
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

        var updated = new WorkTask
        {
            Id = existing.Id,
            Title = trimmed,
            Status = task.Status,
            Notes = string.IsNullOrWhiteSpace(task.Notes) ? null : task.Notes.Trim(),
            CurrentStatus = task.CurrentStatus,
            LastProgress = task.LastProgress,
            NextAction = task.NextAction,
            Blocker = task.Blocker,
            ProjectId = task.ProjectId,
            MilestoneId = task.MilestoneId,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = _clock(),
            LastWorkedAt = task.LastWorkedAt
        };

        _store.Update(updated);
        return updated;
    }

    public void Delete(Guid id) => _store.Delete(id);
}
