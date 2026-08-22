using System.Collections.ObjectModel;
using System.Globalization;
using Jetset.App.Helpers;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class TaskFocusBreakdownItemViewModel
{
    public TaskFocusBreakdownItemViewModel(Guid taskId, string taskTitle, TimeSpan focusTime, int sessionCount)
    {
        TaskId = taskId;
        TaskTitle = taskTitle;
        FocusTime = focusTime;
        SessionCount = sessionCount;
    }

    public Guid TaskId { get; }

    public string TaskTitle { get; }

    public TimeSpan FocusTime { get; }

    public int SessionCount { get; }

    public string FocusTimeText => DurationFormatter.FormatFriendly(FocusTime);

    public string SessionCountText => SessionCount == 1 ? "1 session" : $"{SessionCount} sessions";
}

public sealed class HeatmapDayViewModel
{
    public HeatmapDayViewModel(DateOnly date, int intensityLevel, bool isVisible, string tooltipText)
    {
        Date = date;
        IntensityLevel = intensityLevel;
        IsVisible = isVisible;
        TooltipText = tooltipText;
    }

    public DateOnly Date { get; }

    public int IntensityLevel { get; }

    public bool IsVisible { get; }

    public string TooltipText { get; }
}

public sealed class HeatmapWeekViewModel
{
    public HeatmapWeekViewModel(IReadOnlyList<HeatmapDayViewModel> days)
    {
        Days = days;
    }

    public IReadOnlyList<HeatmapDayViewModel> Days { get; }
}

public sealed class AnalyticsViewModel : ObservableObject
{
    private const int HeatmapWeekCount = 12;

    private readonly AppServices _services;
    private DateTime _selectedDate;
    private string _totalFocusTimeText = string.Empty;
    private string _sessionSummaryText = string.Empty;
    private string _heatmapRangeText = string.Empty;
    private string _currentStreakText = "0 days";
    private string _longestStreakText = "Best: 0 days";

    public AnalyticsViewModel(AppServices services)
    {
        _services = services;
        _selectedDate = _services.Clock.Now.ToLocalTime().Date;
        TaskBreakdown = new ObservableCollection<TaskFocusBreakdownItemViewModel>();
        HeatmapWeeks = new ObservableCollection<HeatmapWeekViewModel>();
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public string Title => "Analytics";

    public ObservableCollection<TaskFocusBreakdownItemViewModel> TaskBreakdown { get; }

    public ObservableCollection<HeatmapWeekViewModel> HeatmapWeeks { get; }

    public RelayCommand RefreshCommand { get; }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                OnPropertyChanged(nameof(DailyFocusDateText));
                Refresh();
            }
        }
    }

    public string TotalFocusTimeText
    {
        get => _totalFocusTimeText;
        private set => SetProperty(ref _totalFocusTimeText, value);
    }

    public string SessionSummaryText
    {
        get => _sessionSummaryText;
        private set => SetProperty(ref _sessionSummaryText, value);
    }

    public string HeatmapRangeText
    {
        get => _heatmapRangeText;
        private set => SetProperty(ref _heatmapRangeText, value);
    }

    public string CurrentStreakText
    {
        get => _currentStreakText;
        private set => SetProperty(ref _currentStreakText, value);
    }

    public string LongestStreakText
    {
        get => _longestStreakText;
        private set => SetProperty(ref _longestStreakText, value);
    }

    public string DailyFocusDateText =>
        SelectedDate.ToString("dddd, MMM d, yyyy", CultureInfo.CurrentCulture);

    public bool HasTaskBreakdown => TaskBreakdown.Count > 0;

    public void Refresh()
    {
        RefreshHeatmap();
        RefreshStreak();

        var localMidnight = new DateTime(
            SelectedDate.Year,
            SelectedDate.Month,
            SelectedDate.Day,
            0,
            0,
            0,
            DateTimeKind.Local);
        var dayReference = new DateTimeOffset(localMidnight);
        var summary = _services.Analytics.GetDailySummary(dayReference);

        TotalFocusTimeText = DurationFormatter.FormatFriendly(summary.TotalFocusTime);
        SessionSummaryText = summary.SessionCount switch
        {
            0 => "No sessions recorded",
            1 => $"1 session ({summary.CompletedSessionCount} completed)",
            _ => $"{summary.SessionCount} sessions ({summary.CompletedSessionCount} completed)"
        };

        TaskBreakdown.Clear();
        foreach (var item in summary.TaskBreakdown)
        {
            TaskBreakdown.Add(new TaskFocusBreakdownItemViewModel(
                item.TaskId,
                item.TaskTitle,
                item.FocusTime,
                item.SessionCount));
        }

        OnPropertyChanged(nameof(HasTaskBreakdown));
    }

    private void RefreshStreak()
    {
        var streak = _services.Analytics.GetStreak();
        CurrentStreakText = FormatStreakDays(streak.CurrentStreak);
        LongestStreakText = $"Best: {FormatStreakDays(streak.LongestStreak)}";
    }

    private static string FormatStreakDays(int days) =>
        days == 1 ? "1 day" : $"{days} days";

    private void RefreshHeatmap()
    {
        var today = DateOnly.FromDateTime(_services.Clock.Now.ToLocalTime().Date);
        var endWeekStart = GetWeekStart(today);
        var startWeekStart = endWeekStart.AddDays(-7 * (HeatmapWeekCount - 1));
        var heatmap = _services.Analytics.GetActivityHeatmap(startWeekStart, today);
        var dayMap = heatmap.Days.ToDictionary(d => d.Date);
        var maxMinutes = heatmap.Days.Max(d => d.FocusMinutes);

        HeatmapWeeks.Clear();
        for (var week = 0; week < HeatmapWeekCount; week++)
        {
            var weekStart = startWeekStart.AddDays(week * 7);
            var days = new List<HeatmapDayViewModel>();

            for (var dayOffset = 0; dayOffset < 7; dayOffset++)
            {
                var date = weekStart.AddDays(dayOffset);
                var isVisible = date <= today;
                var entry = dayMap.GetValueOrDefault(date);
                var focusMinutes = entry?.FocusMinutes ?? 0;
                var focusTime = entry?.FocusTime ?? TimeSpan.Zero;
                var intensity = ComputeIntensity(focusMinutes, maxMinutes);
                var tooltip = BuildHeatmapTooltip(date, focusMinutes, focusTime);

                days.Add(new HeatmapDayViewModel(date, intensity, isVisible, tooltip));
            }

            HeatmapWeeks.Add(new HeatmapWeekViewModel(days));
        }

        HeatmapRangeText = $"{startWeekStart:MMM d} – {today:MMM d, yyyy}";
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var offset = (int)date.DayOfWeek;
        return date.AddDays(-offset);
    }

    private static int ComputeIntensity(int focusMinutes, int maxMinutes)
    {
        if (focusMinutes <= 0)
        {
            return 0;
        }

        if (maxMinutes <= 0)
        {
            return 1;
        }

        var ratio = focusMinutes / (double)maxMinutes;
        if (ratio <= 0.25)
        {
            return 1;
        }

        if (ratio <= 0.5)
        {
            return 2;
        }

        if (ratio <= 0.75)
        {
            return 3;
        }

        return 4;
    }

    private static string BuildHeatmapTooltip(DateOnly date, int focusMinutes, TimeSpan focusTime)
    {
        var dateText = date.ToString("dddd, d MMM", CultureInfo.CurrentCulture);
        if (focusMinutes <= 0)
        {
            return $"{dateText}: No focus time";
        }

        return $"{dateText}: {focusMinutes} min ({DurationFormatter.FormatFriendly(focusTime)})";
    }
}
