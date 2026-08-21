using Jetset.App.Models;
using Jetset.App.Persistence;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.Services;

/// <summary>
/// Read-only aggregation of session data into productivity metrics.
/// </summary>
public sealed class AnalyticsService
{
    private readonly SessionService _sessions;
    private readonly TaskService _tasks;
    private readonly ProjectService _projects;
    private readonly ITaskSwitchEventStore _switchEvents;
    private readonly Func<DateTimeOffset> _clock;

    public AnalyticsService(
        SessionService sessions,
        TaskService tasks,
        ProjectService projects,
        ITaskSwitchEventStore switchEvents,
        Func<DateTimeOffset> clock)
    {
        _sessions = sessions;
        _tasks = tasks;
        _projects = projects;
        _switchEvents = switchEvents;
        _clock = clock;
    }

    public DailySummary GetDailySummary(DateTimeOffset? day = null)
    {
        var reference = day ?? _clock();
        var localDate = DateOnly.FromDateTime(reference.ToLocalTime().Date);
        var sessions = _sessions.GetTodaysSessions(reference);
        var now = day ?? _clock();

        var eligible = sessions.Where(s => s.State != SessionState.Cancelled).ToList();
        var breakdownMap = new Dictionary<Guid, (string Title, TimeSpan Duration, int Count)>();
        var total = TimeSpan.Zero;

        foreach (var session in eligible)
        {
            var duration = _sessions.GetActiveDuration(session.Id, now);
            total += duration;

            if (!breakdownMap.TryGetValue(session.TaskId, out var entry))
            {
                var task = _tasks.Get(session.TaskId);
                entry = (task?.Title ?? session.TaskName, TimeSpan.Zero, 0);
            }

            breakdownMap[session.TaskId] = (entry.Title, entry.Duration + duration, entry.Count + 1);
        }

        var breakdown = breakdownMap
            .Select(pair => new TaskFocusBreakdown
            {
                TaskId = pair.Key,
                TaskTitle = pair.Value.Title,
                FocusTime = pair.Value.Duration,
                SessionCount = pair.Value.Count
            })
            .OrderByDescending(b => b.FocusTime)
            .ThenBy(b => b.TaskTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DailySummary
        {
            Date = localDate,
            TotalFocusTime = total,
            SessionCount = eligible.Count,
            CompletedSessionCount = eligible.Count(s => s.State == SessionState.Completed),
            TaskBreakdown = breakdown
        };
    }

    public IReadOnlyList<DailyFocusTime> GetFocusTime(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        var results = new List<DailyFocusTime>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var localMidnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local);
            var dayReference = new DateTimeOffset(localMidnight);
            var summary = GetDailySummary(dayReference);
            results.Add(new DailyFocusTime
            {
                Date = date,
                FocusTime = summary.TotalFocusTime
            });
        }

        return results;
    }

    public ActivityHeatmap GetActivityHeatmap(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        var days = GetFocusTime(startDate, endDate)
            .Select(d => new ActivityHeatmapDay
            {
                Date = d.Date,
                FocusTime = d.FocusTime,
                FocusMinutes = (int)Math.Round(d.FocusTime.TotalMinutes)
            })
            .ToList();

        return new ActivityHeatmap
        {
            StartDate = startDate,
            EndDate = endDate,
            Days = days
        };
    }

    public TimeSpan GetFocusTimeByTask(Guid taskId)
    {
        var total = TimeSpan.Zero;

        foreach (var session in _sessions.GetSessionsByTaskId(taskId))
        {
            if (session.State == SessionState.Cancelled)
            {
                continue;
            }

            total += _sessions.GetActiveDuration(session.Id);
        }

        return total;
    }

    public ProductivityStreak GetStreak()
    {
        var today = DateOnly.FromDateTime(_clock().ToLocalTime().Date);
        var earliest = FindEarliestSessionDate();
        if (earliest is null)
        {
            return new ProductivityStreak();
        }

        var dailyFocus = GetFocusTime(earliest.Value, today);
        var productiveDays = dailyFocus
            .Where(d => d.FocusTime > TimeSpan.Zero)
            .Select(d => d.Date)
            .ToHashSet();

        var currentStreak = ComputeCurrentStreak(today, productiveDays);
        var longestStreak = ComputeLongestStreak(dailyFocus);

        return new ProductivityStreak
        {
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak
        };
    }

    public ProjectMomentum GetProjectMomentum(Guid projectId, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        var project = _projects.Get(projectId)
            ?? throw new InvalidOperationException($"Project {projectId} was not found.");

        var projectTasks = _tasks.ListByProject(projectId);
        var taskIds = projectTasks.Select(t => t.Id).ToHashSet();
        var weekStarts = EnumerateWeekStarts(startDate, endDate).ToList();
        var focusByWeek = weekStarts.ToDictionary(w => w, _ => TimeSpan.Zero);
        var createdByWeek = weekStarts.ToDictionary(w => w, _ => 0);
        var completedByWeek = weekStarts.ToDictionary(w => w, _ => 0);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var localMidnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local);
            var dayReference = new DateTimeOffset(localMidnight);
            var week = GetWeekStart(date);
            if (!focusByWeek.ContainsKey(week))
            {
                continue;
            }

            foreach (var session in _sessions.GetTodaysSessions(dayReference))
            {
                if (session.State == SessionState.Cancelled || !taskIds.Contains(session.TaskId))
                {
                    continue;
                }

                focusByWeek[week] += _sessions.GetActiveDuration(session.Id, dayReference);
            }
        }

        var totalCreated = 0;
        var totalCompleted = 0;

        foreach (var task in projectTasks)
        {
            var createdDate = DateOnly.FromDateTime(task.CreatedAt.ToLocalTime().Date);
            if (createdDate >= startDate && createdDate <= endDate)
            {
                totalCreated++;
                var createdWeek = GetWeekStart(createdDate);
                if (createdByWeek.ContainsKey(createdWeek))
                {
                    createdByWeek[createdWeek]++;
                }
            }

            if (task.Status != TaskStatus.Done)
            {
                continue;
            }

            var completedDate = DateOnly.FromDateTime(task.UpdatedAt.ToLocalTime().Date);
            if (completedDate < startDate || completedDate > endDate)
            {
                continue;
            }

            totalCompleted++;
            var completedWeek = GetWeekStart(completedDate);
            if (completedByWeek.ContainsKey(completedWeek))
            {
                completedByWeek[completedWeek]++;
            }
        }

        var weeklyFocus = weekStarts
            .Select(week => new WeeklyFocusTime
            {
                WeekStart = week,
                FocusTime = focusByWeek[week],
                FocusMinutes = (int)Math.Round(focusByWeek[week].TotalMinutes)
            })
            .ToList();

        var weeklyCompletion = weekStarts
            .Select(week => new WeeklyTaskCompletion
            {
                WeekStart = week,
                TasksCreated = createdByWeek[week],
                TasksCompleted = completedByWeek[week]
            })
            .ToList();

        return new ProjectMomentum
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            StartDate = startDate,
            EndDate = endDate,
            WeeklyFocusTrend = weeklyFocus,
            WeeklyCompletion = weeklyCompletion,
            TotalTasksCreated = totalCreated,
            TotalTasksCompleted = totalCompleted
        };
    }

    public SwitchMetrics GetSwitchMetrics(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        var startInclusive = ToLocalMidnight(startDate);
        var endExclusive = ToLocalMidnight(endDate.AddDays(1));
        var events = _switchEvents.ListBetween(startInclusive, endExclusive);

        var dailyMap = new Dictionary<DateOnly, int>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            dailyMap[date] = 0;
        }

        var hourCounts = new int[24];

        foreach (var switchEvent in events)
        {
            var local = switchEvent.OccurredAt.ToLocalTime();
            var date = DateOnly.FromDateTime(local.DateTime);
            if (dailyMap.ContainsKey(date))
            {
                dailyMap[date]++;
            }

            hourCounts[local.Hour]++;
        }

        var total = events.Count;
        var dayCount = endDate.DayNumber - startDate.DayNumber + 1;
        int? busiestHour = total > 0
            ? hourCounts.Select((count, hour) => (count, hour)).MaxBy(pair => pair.count).hour
            : null;

        var dailyCounts = dailyMap
            .OrderBy(pair => pair.Key)
            .Select(pair => new DailySwitchCount
            {
                Date = pair.Key,
                SwitchCount = pair.Value
            })
            .ToList();

        return new SwitchMetrics
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalSwitchCount = total,
            AveragePerDay = dayCount > 0 ? total / (double)dayCount : 0,
            BusiestHour = busiestHour,
            DailyCounts = dailyCounts
        };
    }

    private static DateTimeOffset ToLocalMidnight(DateOnly date)
    {
        var localMidnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local);
        return new DateTimeOffset(localMidnight);
    }

    private DateOnly? FindEarliestSessionDate()
    {
        DateOnly? earliest = null;

        foreach (var task in _tasks.List())
        {
            foreach (var session in _sessions.GetSessionsByTaskId(task.Id))
            {
                if (session.State == SessionState.Cancelled)
                {
                    continue;
                }

                var day = DateOnly.FromDateTime(session.StartedAt.ToLocalTime().Date);
                if (earliest is null || day < earliest)
                {
                    earliest = day;
                }
            }
        }

        return earliest;
    }

    private static int ComputeCurrentStreak(DateOnly today, HashSet<DateOnly> productiveDays)
    {
        var probe = productiveDays.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;

        while (productiveDays.Contains(probe))
        {
            streak++;
            probe = probe.AddDays(-1);
        }

        return streak;
    }

    private static int ComputeLongestStreak(IReadOnlyList<DailyFocusTime> dailyFocus)
    {
        var longest = 0;
        var current = 0;

        foreach (var day in dailyFocus)
        {
            if (day.FocusTime > TimeSpan.Zero)
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 0;
            }
        }

        return longest;
    }

    private static IEnumerable<DateOnly> EnumerateWeekStarts(DateOnly startDate, DateOnly endDate)
    {
        var weekStart = GetWeekStart(startDate);
        var endWeekStart = GetWeekStart(endDate);

        for (var week = weekStart; week <= endWeekStart; week = week.AddDays(7))
        {
            yield return week;
        }
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var offset = (int)date.DayOfWeek;
        return date.AddDays(-offset);
    }
}
