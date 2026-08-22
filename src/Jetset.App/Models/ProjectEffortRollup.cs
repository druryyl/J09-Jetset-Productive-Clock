namespace Jetset.App.Models;

/// <summary>
/// Derived effort totals for a project (not persisted).
/// </summary>
public sealed class ProjectEffortRollup
{
    public TimeSpan Spent { get; init; }

    /// <summary>
    /// Sum of child task estimates; null when no child has an estimate.
    /// </summary>
    public int? EstimateMinutes { get; init; }
}
