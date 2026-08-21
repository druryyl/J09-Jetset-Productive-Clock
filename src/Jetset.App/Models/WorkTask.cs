namespace Jetset.App.Models;

public sealed class WorkTask
{
    public Guid Id { get; init; }

    public string Title { get; set; } = string.Empty;

    public TaskStatus Status { get; set; }

    public string? Notes { get; set; }

    public string? CurrentStatus { get; set; }

    public string? LastProgress { get; set; }

    public string? NextAction { get; set; }

    public string? Blocker { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? MilestoneId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastWorkedAt { get; set; }
}
