using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.Persistence;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly Dictionary<Guid, WorkSession> _sessions = new();
    private readonly Dictionary<Guid, List<WorkInterval>> _intervals = new();

    public WorkSession? GetActiveSession()
    {
        return _sessions.Values.FirstOrDefault(s =>
            s.State is SessionState.Running or SessionState.Paused);
    }

    public IReadOnlyList<WorkInterval> GetIntervals(Guid sessionId)
    {
        return _intervals.TryGetValue(sessionId, out var list)
            ? list.OrderBy(i => i.StartedAt).ToList()
            : [];
    }

    public void SaveNewSession(WorkSession session, WorkInterval firstInterval)
    {
        if (GetActiveSession() is not null)
        {
            throw new InvalidOperationException("Only one active work session is allowed.");
        }

        _sessions[session.Id] = Clone(session);
        _intervals[session.Id] = [Clone(firstInterval)];
    }

    public void UpdateSession(WorkSession session)
    {
        _sessions[session.Id] = Clone(session);
    }

    public void InsertInterval(WorkInterval interval)
    {
        if (!_intervals.TryGetValue(interval.WorkSessionId, out var list))
        {
            list = [];
            _intervals[interval.WorkSessionId] = list;
        }

        list.Add(Clone(interval));
    }

    public void CloseInterval(Guid intervalId, DateTimeOffset endedAt)
    {
        foreach (var list in _intervals.Values)
        {
            var interval = list.FirstOrDefault(i => i.Id == intervalId);
            if (interval is null)
            {
                continue;
            }

            interval.EndedAt = endedAt;
            return;
        }
    }

    public WorkInterval? GetOpenInterval(Guid sessionId)
    {
        if (!_intervals.TryGetValue(sessionId, out var list))
        {
            return null;
        }

        return list.LastOrDefault(i => i.EndedAt is null);
    }

    public IReadOnlyList<WorkSession> GetSessionsForLocalDay(DateTimeOffset day)
    {
        var local = day.ToLocalTime();
        var start = new DateTimeOffset(local.Date, local.Offset);
        var end = start.AddDays(1);

        return _sessions.Values
            .Where(s => s.StartedAt >= start && s.StartedAt < end)
            .OrderBy(s => s.StartedAt)
            .Select(Clone)
            .ToList();
    }

    public TimeSpan GetActiveDuration(Guid sessionId, DateTimeOffset? now = null)
    {
        return SessionCalculations.CalculateActiveDuration(GetIntervals(sessionId), now);
    }

    public void UpdateSessionDetails(WorkSession session, IReadOnlyList<WorkInterval> intervals)
    {
        _sessions[session.Id] = Clone(session);
        _intervals[session.Id] = intervals.Select(Clone).ToList();
    }

    public void DeleteSession(Guid sessionId)
    {
        _sessions.Remove(sessionId);
        _intervals.Remove(sessionId);
    }

    private static WorkSession Clone(WorkSession s) => new()
    {
        Id = s.Id,
        TaskName = s.TaskName,
        Mode = s.Mode,
        StartedAt = s.StartedAt,
        FinishedAt = s.FinishedAt,
        CountdownDuration = s.CountdownDuration,
        State = s.State,
        Note = s.Note,
        LastHeartbeatAt = s.LastHeartbeatAt,
        CountdownEndsAt = s.CountdownEndsAt,
        CountdownRemaining = s.CountdownRemaining,
        CountdownCompletedNotified = s.CountdownCompletedNotified
    };

    private static WorkInterval Clone(WorkInterval i) => new()
    {
        Id = i.Id,
        WorkSessionId = i.WorkSessionId,
        StartedAt = i.StartedAt,
        EndedAt = i.EndedAt
    };
}
