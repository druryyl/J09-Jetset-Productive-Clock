using Jetset.App.Models;



namespace Jetset.App.Services;



/// <summary>

/// Read-only aggregation of session data into productivity metrics.

/// </summary>

public sealed class AnalyticsService

{

    private readonly SessionService _sessions;

    private readonly TaskService _tasks;

    private readonly Func<DateTimeOffset> _clock;



    public AnalyticsService(

        SessionService sessions,

        TaskService tasks,

        Func<DateTimeOffset> clock)

    {

        _sessions = sessions;

        _tasks = tasks;

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

}


