using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

public sealed class ProjectService
{
    private readonly IProjectStore _store;
    private readonly ITaskStore _taskStore;
    private readonly IMilestoneStore? _milestoneStore;
    private readonly Func<DateTimeOffset> _clock;

    public ProjectService(
        IProjectStore store,
        ITaskStore taskStore,
        Func<DateTimeOffset>? clock = null)
        : this(store, taskStore, milestoneStore: null, clock)
    {
    }

    public ProjectService(
        IProjectStore store,
        ITaskStore taskStore,
        IMilestoneStore? milestoneStore,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _taskStore = taskStore;
        _milestoneStore = milestoneStore;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public Project Create(string name, DateOnly? deadline = null)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        var now = _clock();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            Deadline = deadline,
            CreatedAt = now,
            UpdatedAt = now
        };

        _store.Insert(project);
        return project;
    }

    public Project? Get(Guid id) => _store.Get(id);

    public IReadOnlyList<ProjectSummary> List() => _store.ListWithTaskCounts();

    public IReadOnlyList<Project> ListProjects() => _store.List();

    public Project Update(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var trimmed = project.Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Project name is required.", nameof(project));
        }

        var existing = _store.Get(project.Id)
            ?? throw new InvalidOperationException($"Project {project.Id} was not found.");

        var updated = new Project
        {
            Id = existing.Id,
            Name = trimmed,
            Deadline = project.Deadline,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = _clock()
        };

        _store.Update(updated);
        return updated;
    }

    public void Delete(Guid id)
    {
        if (_store.Get(id) is null)
        {
            throw new InvalidOperationException($"Project {id} was not found.");
        }

        _taskStore.UnassignAllFromProject(id);
        _milestoneStore?.DeleteByProject(id);
        _store.Delete(id);
    }
}
