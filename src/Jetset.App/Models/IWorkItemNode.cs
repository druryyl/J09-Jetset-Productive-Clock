namespace Jetset.App.Models;

/// <summary>
/// Conceptual union node for the Work Tree (Task or Project).
/// </summary>
public interface IWorkItemNode
{
    Guid Id { get; }

    WorkItemKind Kind { get; }

    string DisplayName { get; }

    /// <summary>
    /// Owning project for tasks; always null for projects (Option A hierarchy).
    /// </summary>
    Guid? ParentProjectId { get; }
}
