namespace Jetset.App.Models;

public sealed class WorkInterval
{
    public Guid Id { get; init; }

    public Guid WorkSessionId { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; set; }
}
