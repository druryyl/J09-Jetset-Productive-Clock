using Jetset.App.Models;

namespace Jetset.App.Helpers;

public static class SessionCalculations
{
    public static TimeSpan CalculateActiveDuration(
        IReadOnlyList<WorkInterval> intervals,
        DateTimeOffset? now = null)
    {
        var clock = now ?? DateTimeOffset.Now;
        var total = TimeSpan.Zero;

        foreach (var interval in intervals)
        {
            var end = interval.EndedAt ?? clock;
            if (end < interval.StartedAt)
            {
                continue;
            }

            total += end - interval.StartedAt;
        }

        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }

    public static TimeSpan GetCountdownRemaining(WorkSession session, DateTimeOffset now)
    {
        if (session.Mode != TimerMode.Countdown)
        {
            return TimeSpan.Zero;
        }

        if (session.State == SessionState.Paused)
        {
            return session.CountdownRemaining ?? session.CountdownDuration ?? TimeSpan.Zero;
        }

        if (session.CountdownEndsAt is { } endsAt)
        {
            return endsAt - now;
        }

        return session.CountdownRemaining ?? session.CountdownDuration ?? TimeSpan.Zero;
    }

    public static bool IsOvertime(WorkSession session, DateTimeOffset now)
    {
        if (session.Mode != TimerMode.Countdown || session.State != SessionState.Running)
        {
            return false;
        }

        return GetCountdownRemaining(session, now) < TimeSpan.Zero;
    }

    public static TimeSpan GetOvertime(WorkSession session, DateTimeOffset now)
    {
        var remaining = GetCountdownRemaining(session, now);
        return remaining < TimeSpan.Zero ? remaining.Duration() : TimeSpan.Zero;
    }

    public static TimeSpan SumCompletedDurations(
        IEnumerable<(SessionState State, TimeSpan Duration)> sessions)
    {
        var total = TimeSpan.Zero;
        foreach (var (state, duration) in sessions)
        {
            if (state is SessionState.Completed or SessionState.Running or SessionState.Paused)
            {
                total += duration;
            }
        }

        return total;
    }
}
