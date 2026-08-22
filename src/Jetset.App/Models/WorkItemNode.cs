namespace Jetset.App.Models;

public sealed class WorkItemNode : IWorkItemNode
{
    public Guid Id { get; init; }

    public WorkItemKind Kind { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public Guid? ParentProjectId { get; init; }

    public static WorkItemNode FromTask(WorkTask task) => new()
    {
        Id = task.Id,
        Kind = WorkItemKind.Task,
        DisplayName = task.Title,
        ParentProjectId = task.ProjectId
    };

    public static WorkItemNode FromProject(Project project) => new()
    {
        Id = project.Id,
        Kind = WorkItemKind.Project,
        DisplayName = project.Name,
        ParentProjectId = null
    };
}
