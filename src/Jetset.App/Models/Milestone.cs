namespace Jetset.App.Models;

public sealed class Milestone
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
