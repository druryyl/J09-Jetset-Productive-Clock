using System.Globalization;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class ProjectOptionViewModel
{
    public ProjectOptionViewModel(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string Name { get; }
}

public sealed class ProjectMomentumWeekItemViewModel
{
    public ProjectMomentumWeekItemViewModel(
        DateOnly weekStart,
        TimeSpan focusTime,
        int maxFocusMinutes,
        int tasksCreated,
        int tasksCompleted)
    {
        WeekLabel = weekStart.ToString("MMM d", CultureInfo.CurrentCulture);
        FocusTime = focusTime;
        FocusTimeText = DurationFormatter.FormatFriendly(focusTime);
        FocusBarWidth = ComputeBarWidth((int)Math.Round(focusTime.TotalMinutes), maxFocusMinutes);
        TasksCreated = tasksCreated;
        TasksCompleted = tasksCompleted;
        CompletionText = $"{tasksCompleted} done / {tasksCreated} new";
        CompletionBarWidth = ComputeCompletionBarWidth(tasksCreated, tasksCompleted);
    }

    public string WeekLabel { get; }

    public TimeSpan FocusTime { get; }

    public string FocusTimeText { get; }

    public double FocusBarWidth { get; }

    public int TasksCreated { get; }

    public int TasksCompleted { get; }

    public string CompletionText { get; }

    public double CompletionBarWidth { get; }

    private static double ComputeBarWidth(int value, int maxValue)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (maxValue <= 0)
        {
            return ProjectMomentumPresenter.MinBarWidth;
        }

        return Math.Max(
            ProjectMomentumPresenter.MinBarWidth,
            value / (double)maxValue * ProjectMomentumPresenter.MaxBarWidth);
    }

    private static double ComputeCompletionBarWidth(int created, int completed)
    {
        if (completed <= 0)
        {
            return 0;
        }

        if (created <= 0)
        {
            return ProjectMomentumPresenter.MaxBarWidth;
        }

        return Math.Max(
            ProjectMomentumPresenter.MinBarWidth,
            completed / (double)created * ProjectMomentumPresenter.MaxBarWidth);
    }
}

public static class ProjectMomentumPresenter
{
    public const int MomentumWeekCount = 8;
    public const double MaxBarWidth = 160;
    public const double MinBarWidth = 4;

    public static (DateOnly StartDate, DateOnly EndDate) GetDefaultRange(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.ToLocalTime().Date);
        var endWeekStart = GetWeekStart(today);
        var startWeekStart = endWeekStart.AddDays(-7 * (MomentumWeekCount - 1));
        return (startWeekStart, today);
    }

    public static string FormatRangeText(DateOnly startDate, DateOnly endDate) =>
        $"{startDate:MMM d} – {endDate:MMM d, yyyy}";

    public static string FormatSummaryText(ProjectMomentum momentum) =>
        momentum.TotalTasksCompleted == 1 && momentum.TotalTasksCreated == 1
            ? "1 task completed / 1 created"
            : $"{momentum.TotalTasksCompleted} tasks completed / {momentum.TotalTasksCreated} created";

    public static IReadOnlyList<ProjectMomentumWeekItemViewModel> MapWeeks(ProjectMomentum momentum)
    {
        var completionByWeek = momentum.WeeklyCompletion.ToDictionary(w => w.WeekStart);
        var maxFocusMinutes = momentum.WeeklyFocusTrend.Max(w => w.FocusMinutes);

        return momentum.WeeklyFocusTrend
            .Select(week =>
            {
                completionByWeek.TryGetValue(week.WeekStart, out var completion);
                return new ProjectMomentumWeekItemViewModel(
                    week.WeekStart,
                    week.FocusTime,
                    maxFocusMinutes,
                    completion?.TasksCreated ?? 0,
                    completion?.TasksCompleted ?? 0);
            })
            .ToList();
    }

    public static ProjectMomentum? Load(AppServices services, Guid projectId)
    {
        var (startDate, endDate) = GetDefaultRange(services.Clock.Now);
        return services.Analytics.GetProjectMomentum(projectId, startDate, endDate);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var offset = (int)date.DayOfWeek;
        return date.AddDays(-offset);
    }
}
