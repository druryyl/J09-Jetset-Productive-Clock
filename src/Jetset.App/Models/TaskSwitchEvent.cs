namespace Jetset.App.Models;

public sealed class TaskSwitchEvent
{
    public required Guid Id { get; init; }

    public Guid? FromTaskId { get; init; }

    public required Guid ToTaskId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
