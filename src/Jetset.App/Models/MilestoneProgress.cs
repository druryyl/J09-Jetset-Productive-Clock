namespace Jetset.App.Models;

public sealed class MilestoneProgress
{
    public int DoneCount { get; init; }

    public int TotalCount { get; init; }

    public double Fraction => TotalCount == 0 ? 0 : (double)DoneCount / TotalCount;
}
