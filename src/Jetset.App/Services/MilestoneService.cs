using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

public sealed class MilestoneService
{
    private readonly IMilestoneStore _store;
    private readonly IProjectStore _projectStore;
    private readonly ITaskStore _taskStore;
    private readonly Func<DateTimeOffset> _clock;

    public MilestoneService(
        IMilestoneStore store,
        IProjectStore projectStore,
        ITaskStore taskStore,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _projectStore = projectStore;
        _taskStore = taskStore;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public Milestone Create(Guid projectId, string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Milestone name is required.", nameof(name));
        }

        EnsureProjectExists(projectId);

        var existing = _store.ListByProject(projectId);
        var sortOrder = existing.Count == 0 ? 0 : existing.Max(m => m.SortOrder) + 1;

        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = trimmed,
            SortOrder = sortOrder,
            CreatedAt = _clock()
        };

        _store.Insert(milestone);
        return milestone;
    }

    public Milestone? Get(Guid id) => _store.Get(id);

    public IReadOnlyList<Milestone> ListByProject(Guid projectId) =>
        _store.ListByProject(projectId);

    public Milestone Update(Milestone milestone)
    {
        ArgumentNullException.ThrowIfNull(milestone);

        var trimmed = milestone.Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Milestone name is required.", nameof(milestone));
        }

        var existing = _store.Get(milestone.Id)
            ?? throw new InvalidOperationException($"Milestone {milestone.Id} was not found.");

        var updated = new Milestone
        {
            Id = existing.Id,
            ProjectId = existing.ProjectId,
            Name = trimmed,
            SortOrder = existing.SortOrder,
            CreatedAt = existing.CreatedAt
        };

        _store.Update(updated);
        return updated;
    }

    public void Delete(Guid id)
    {
        if (_store.Get(id) is null)
        {
            throw new InvalidOperationException($"Milestone {id} was not found.");
        }

        _taskStore.UnassignAllFromMilestone(id);
        _store.Delete(id);
    }

    public void DeleteByProject(Guid projectId) => _store.DeleteByProject(projectId);

    public void Reorder(Guid projectId, IReadOnlyList<Guid> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        EnsureProjectExists(projectId);

        var existing = _store.ListByProject(projectId);
        if (existing.Count != orderedIds.Count)
        {
            throw new ArgumentException(
                "Ordered list must include every milestone in the project.",
                nameof(orderedIds));
        }

        var existingIds = existing.Select(m => m.Id).ToHashSet();
        if (!orderedIds.All(existingIds.Contains) || orderedIds.Distinct().Count() != orderedIds.Count)
        {
            throw new ArgumentException(
                "Ordered list must contain each project milestone exactly once.",
                nameof(orderedIds));
        }

        _store.UpdateSortOrders(projectId, orderedIds);
    }

    public MilestoneProgress GetProgress(Guid milestoneId)
    {
        if (_store.Get(milestoneId) is null)
        {
            throw new InvalidOperationException($"Milestone {milestoneId} was not found.");
        }

        return new MilestoneProgress
        {
            DoneCount = _taskStore.CountDoneByMilestone(milestoneId),
            TotalCount = _taskStore.CountByMilestone(milestoneId)
        };
    }

    private void EnsureProjectExists(Guid projectId)
    {
        if (_projectStore.Get(projectId) is null)
        {
            throw new InvalidOperationException($"Project {projectId} was not found.");
        }
    }
}
