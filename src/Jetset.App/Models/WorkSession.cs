namespace Jetset.App.Models;

public sealed class WorkSession
{
    public Guid Id { get; init; }

    public Guid TaskId { get; init; }

    public string TaskName { get; set; } = string.Empty;

    public TimerMode Mode { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; set; }

    public TimeSpan? CountdownDuration { get; init; }

    public SessionState State { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Last persisted activity timestamp used for crash recovery.
    /// </summary>
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    /// <summary>
    /// Absolute countdown end while running. Null when paused or stopwatch.
    /// </summary>
    public DateTimeOffset? CountdownEndsAt { get; set; }

    /// <summary>
    /// Remaining countdown when paused (or initially configured).
    /// </summary>
    public TimeSpan? CountdownRemaining { get; set; }

    public bool CountdownCompletedNotified { get; set; }
}
