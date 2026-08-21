namespace Jetset.App.Models;

/// <summary>
/// Editable working-context fields captured when leaving a task.
/// </summary>
public sealed class WorkingContext
{
    public string? CurrentStatus { get; init; }

    public string? LastProgress { get; init; }

    public string? NextAction { get; init; }

    public string? Blocker { get; init; }

    public string? Notes { get; init; }

    public static WorkingContext FromTask(WorkTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return new WorkingContext
        {
            CurrentStatus = task.CurrentStatus,
            LastProgress = task.LastProgress,
            NextAction = task.NextAction,
            Blocker = task.Blocker,
            Notes = task.Notes
        };
    }
}
