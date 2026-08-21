namespace Jetset.App.Models;

public sealed class ProjectSummary
{
    public required Project Project { get; init; }

    public int TaskCount { get; init; }
}
