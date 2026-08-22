using Jetset.App.Models;

namespace Jetset.App.Persistence;

public sealed class InMemoryProjectStore : IProjectStore
{
    private readonly Dictionary<Guid, Project> _projects = new();
    private readonly Func<IReadOnlyList<WorkTask>>? _listTasks;

    public InMemoryProjectStore(Func<IReadOnlyList<WorkTask>>? listTasks = null)
    {
        _listTasks = listTasks;
    }

    public Project? Get(Guid id) =>
        _projects.TryGetValue(id, out var project) ? Clone(project) : null;

    public IReadOnlyList<Project> List() =>
        _projects.Values
            .OrderByDescending(p => p.UpdatedAt)
            .Select(Clone)
            .ToList();

    public IReadOnlyList<ProjectSummary> ListWithTaskCounts()
    {
        var tasks = _listTasks?.Invoke() ?? [];
        return _projects.Values
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new ProjectSummary
            {
                Project = Clone(p),
                TaskCount = tasks.Count(t => t.ProjectId == p.Id)
            })
            .ToList();
    }

    public void Insert(Project project) => _projects[project.Id] = Clone(project);

    public void Update(Project project)
    {
        if (!_projects.ContainsKey(project.Id))
        {
            throw new InvalidOperationException($"Project {project.Id} was not found.");
        }

        _projects[project.Id] = Clone(project);
    }

    public void Delete(Guid id) => _projects.Remove(id);

    private static Project Clone(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Deadline = p.Deadline,
        ContextText = p.ContextText,
        ContextUpdatedAt = p.ContextUpdatedAt,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
