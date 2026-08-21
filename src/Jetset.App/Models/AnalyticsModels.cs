namespace Jetset.App.Models;

public sealed class DailySummary
{
    public required DateOnly Date { get; init; }

    public TimeSpan TotalFocusTime { get; init; }

    public int SessionCount { get; init; }

    public int CompletedSessionCount { get; init; }

    public IReadOnlyList<TaskFocusBreakdown> TaskBreakdown { get; init; } = [];
}

public sealed class TaskFocusBreakdown
{
    public required Guid TaskId { get; init; }

    public required string TaskTitle { get; init; }

    public TimeSpan FocusTime { get; init; }

    public int SessionCount { get; init; }
}

public sealed class DailyFocusTime
{
    public required DateOnly Date { get; init; }

    public TimeSpan FocusTime { get; init; }
}

public sealed class ActivityHeatmapDay
{
    public required DateOnly Date { get; init; }

    public TimeSpan FocusTime { get; init; }

    public int FocusMinutes { get; init; }
}

public sealed class ActivityHeatmap
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public IReadOnlyList<ActivityHeatmapDay> Days { get; init; } = [];
}

public sealed class ProductivityStreak
{
    public int CurrentStreak { get; init; }

    public int LongestStreak { get; init; }
}

public sealed class WeeklyFocusTime
{
    public required DateOnly WeekStart { get; init; }

    public TimeSpan FocusTime { get; init; }

    public int FocusMinutes { get; init; }
}

public sealed class WeeklyTaskCompletion
{
    public required DateOnly WeekStart { get; init; }

    public int TasksCreated { get; init; }

    public int TasksCompleted { get; init; }
}

public sealed class ProjectMomentum
{
    public required Guid ProjectId { get; init; }

    public required string ProjectName { get; init; }

    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public IReadOnlyList<WeeklyFocusTime> WeeklyFocusTrend { get; init; } = [];

    public IReadOnlyList<WeeklyTaskCompletion> WeeklyCompletion { get; init; } = [];

    public int TotalTasksCreated { get; init; }

    public int TotalTasksCompleted { get; init; }
}

public sealed class DailySwitchCount
{
    public required DateOnly Date { get; init; }

    public int SwitchCount { get; init; }
}

public sealed class SwitchMetrics
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public int TotalSwitchCount { get; init; }

    public double AveragePerDay { get; init; }

    public int? BusiestHour { get; init; }

    public IReadOnlyList<DailySwitchCount> DailyCounts { get; init; } = [];
}
