namespace Jetset.App.Models;

/// <summary>
/// A task with resolved project display name for resume entry points.
/// </summary>
public sealed class TaskWithContext
{
    public required WorkTask Task { get; init; }

    public string? ProjectName { get; init; }
}
