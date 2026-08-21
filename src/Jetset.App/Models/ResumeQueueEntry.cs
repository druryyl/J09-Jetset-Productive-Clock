namespace Jetset.App.Models;

/// <summary>
/// A task-centric resume queue item derived from task state and paused sessions.
/// </summary>
public sealed class ResumeQueueEntry
{
    public required WorkTask Task { get; init; }

    public WorkSession? PausedSession { get; init; }
}
