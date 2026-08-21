namespace Jetset.App.Models;

public sealed class ContextSnapshot
{
    public Guid Id { get; init; }

    public Guid TaskId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public string? CurrentStatus { get; set; }

    public string? LastProgress { get; set; }

    public string? NextAction { get; set; }

    public string? Blocker { get; set; }

    public string? Notes { get; set; }
}
