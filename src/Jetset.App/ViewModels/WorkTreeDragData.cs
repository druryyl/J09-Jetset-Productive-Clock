namespace Jetset.App.ViewModels;

/// <summary>
/// Payload for Work Tree drag-and-drop operations.
/// </summary>
public sealed class WorkTreeDragData
{
    public const string Format = "Jetset.WorkTree.TaskId";

    public WorkTreeDragData(Guid taskId)
    {
        TaskId = taskId;
    }

    public Guid TaskId { get; }
}
