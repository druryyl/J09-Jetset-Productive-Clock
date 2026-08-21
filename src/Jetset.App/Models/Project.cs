namespace Jetset.App.Models;

public sealed class Project
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public DateOnly? Deadline { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }
}
