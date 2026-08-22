using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

/// <summary>
/// Read-only Work Tree queries (Option A: Project → Task only).
/// </summary>
public sealed class WorkTreeService
{
    private readonly ITaskStore _taskStore;
    private readonly IProjectStore _projectStore;

    public WorkTreeService(ITaskStore taskStore, IProjectStore projectStore)
    {
        _taskStore = taskStore;
        _projectStore = projectStore;
    }

    public IReadOnlyList<IWorkItemNode> ListRootWorkItems()
    {
        var projects = _projectStore.List()
            .Select(WorkItemNode.FromProject)
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase);

        var standaloneTasks = _taskStore.ListByProject(null)
            .Select(WorkItemNode.FromTask)
            .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase);

        return projects.Cast<IWorkItemNode>()
            .Concat(standaloneTasks)
            .ToList();
    }

    public IReadOnlyList<IWorkItemNode> GetChildren(Guid projectId)
    {
        return _taskStore.ListByProject(projectId)
            .Select(WorkItemNode.FromTask)
            .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Cast<IWorkItemNode>()
            .ToList();
    }
}
