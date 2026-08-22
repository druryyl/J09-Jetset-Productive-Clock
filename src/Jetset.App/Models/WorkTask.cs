namespace Jetset.App.Models;

public sealed class WorkTask
{
    public Guid Id { get; init; }

    public string Title { get; set; } = string.Empty;

    public TaskStatus Status { get; set; }

    public TaskOrigin Origin { get; set; }

    public string? Notes { get; set; }

    public Guid? ProjectId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastWorkedAt { get; set; }
}
