using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

public sealed class SessionService
{
    private readonly ISessionStore _store;
    private readonly Func<DateTimeOffset> _clock;

    public SessionService(ISessionStore store, Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public WorkSession? ActiveSession => _store.GetActiveSession();

    public bool HasActiveSession => ActiveSession is not null;

    public event EventHandler? SessionChanged;

    public WorkSession Start(string taskName, TimerMode mode, TimeSpan? countdownDuration)
    {
        var name = taskName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name is required.", nameof(taskName));
        }

        if (_store.GetActiveSession() is not null)
        {
            throw new InvalidOperationException("Only one active work session is allowed.");
        }

        if (mode == TimerMode.Countdown)
        {
            if (countdownDuration is null || countdownDuration <= TimeSpan.Zero)
            {
                throw new ArgumentException("Countdown duration must be greater than zero.", nameof(countdownDuration));
            }
        }

        var now = _clock();
        var session = new WorkSession
        {
            Id = Guid.NewGuid(),
            TaskName = name,
            Mode = mode,
            StartedAt = now,
            CountdownDuration = mode == TimerMode.Countdown ? countdownDuration : null,
            CountdownRemaining = mode == TimerMode.Countdown ? countdownDuration : null,
            CountdownEndsAt = mode == TimerMode.Countdown ? now.Add(countdownDuration!.Value) : null,
            State = SessionState.Running,
            LastHeartbeatAt = now
        };

        var interval = new WorkInterval
        {
            Id = Guid.NewGuid(),
            WorkSessionId = session.Id,
            StartedAt = now
        };

        _store.SaveNewSession(session, interval);
        RaiseChanged();
        return session;
    }

    public void Pause()
    {
        var session = RequireActive();
        if (session.State == SessionState.Paused)
        {
            throw new InvalidOperationException("Session is already paused.");
        }

        if (session.State != SessionState.Running)
        {
            throw new InvalidOperationException("Only a running session can be paused.");
        }

        var now = _clock();
        var open = _store.GetOpenInterval(session.Id);
        if (open is not null)
        {
            _store.CloseInterval(open.Id, now);
        }

        if (session.Mode == TimerMode.Countdown)
        {
            var remaining = SessionCalculations.GetCountdownRemaining(session, now);
            session.CountdownRemaining = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            session.CountdownEndsAt = null;
        }

        session.State = SessionState.Paused;
        session.LastHeartbeatAt = now;
        _store.UpdateSession(session);
        RaiseChanged();
    }

    public void Resume()
    {
        var session = RequireActive();
        if (session.State == SessionState.Running)
        {
            throw new InvalidOperationException("Session is already running.");
        }

        if (session.State != SessionState.Paused)
        {
            throw new InvalidOperationException("Only a paused session can be resumed.");
        }

        if (_store.GetOpenInterval(session.Id) is not null)
        {
            throw new InvalidOperationException("Cannot resume while an open interval already exists.");
        }

        var now = _clock();
        if (session.Mode == TimerMode.Countdown)
        {
            var remaining = session.CountdownRemaining ?? TimeSpan.Zero;
            session.CountdownEndsAt = now.Add(remaining);
        }

        var interval = new WorkInterval
        {
            Id = Guid.NewGuid(),
            WorkSessionId = session.Id,
            StartedAt = now
        };

        session.State = SessionState.Running;
        session.LastHeartbeatAt = now;
        _store.InsertInterval(interval);
        _store.UpdateSession(session);
        RaiseChanged();
    }

    public WorkSession Finish(string? note = null, DateTimeOffset? finishedAt = null)
    {
        var session = RequireActive();
        var now = finishedAt ?? _clock();

        var open = _store.GetOpenInterval(session.Id);
        if (open is not null)
        {
            var end = now < open.StartedAt ? open.StartedAt : now;
            _store.CloseInterval(open.Id, end);
        }

        session.State = SessionState.Completed;
        session.FinishedAt = now;
        session.Note = string.IsNullOrWhiteSpace(note) ? session.Note : note.Trim();
        session.CountdownEndsAt = null;
        session.LastHeartbeatAt = now;
        _store.UpdateSession(session);
        RaiseChanged();
        return session;
    }

    public WorkSession Discard()
    {
        var session = RequireActive();
        var now = _clock();

        var open = _store.GetOpenInterval(session.Id);
        if (open is not null)
        {
            _store.CloseInterval(open.Id, now);
        }

        session.State = SessionState.Cancelled;
        session.FinishedAt = now;
        session.CountdownEndsAt = null;
        session.LastHeartbeatAt = now;
        _store.UpdateSession(session);
        RaiseChanged();
        return session;
    }

    public WorkSession ContinueRecovered()
    {
        var session = RequireActive();
        var now = _clock();

        if (session.State == SessionState.Running)
        {
            var open = _store.GetOpenInterval(session.Id);
            if (open is not null)
            {
                var end = session.LastHeartbeatAt ?? open.StartedAt;
                if (end < open.StartedAt)
                {
                    end = open.StartedAt;
                }

                // Close at last known activity so the crash/offline gap is not counted.
                if (end > open.StartedAt)
                {
                    _store.CloseInterval(open.Id, end);
                }
                else
                {
                    _store.CloseInterval(open.Id, open.StartedAt);
                }
            }

            _store.InsertInterval(new WorkInterval
            {
                Id = Guid.NewGuid(),
                WorkSessionId = session.Id,
                StartedAt = now
            });

            if (session.Mode == TimerMode.Countdown)
            {
                var remaining = session.CountdownRemaining
                    ?? SessionCalculations.GetCountdownRemaining(session, session.LastHeartbeatAt ?? now);
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                session.CountdownRemaining = remaining;
                session.CountdownEndsAt = now.Add(remaining);
            }
        }
        else if (session.State == SessionState.Paused)
        {
            var open = _store.GetOpenInterval(session.Id);
            if (open is not null)
            {
                var end = session.LastHeartbeatAt ?? open.StartedAt;
                if (end < open.StartedAt)
                {
                    end = open.StartedAt;
                }

                _store.CloseInterval(open.Id, end);
            }
        }

        session.LastHeartbeatAt = now;
        _store.UpdateSession(session);
        RaiseChanged();
        return session;
    }

    public WorkSession FinishAtLastKnownActivity()
    {
        var session = RequireActive();
        var finishAt = session.LastHeartbeatAt
            ?? _store.GetIntervals(session.Id).LastOrDefault()?.EndedAt
            ?? _store.GetIntervals(session.Id).LastOrDefault()?.StartedAt
            ?? session.StartedAt;

        return Finish(finishedAt: finishAt);
    }

    public void Heartbeat()
    {
        var session = _store.GetActiveSession();
        if (session is null || session.State != SessionState.Running)
        {
            return;
        }

        session.LastHeartbeatAt = _clock();
        _store.UpdateSession(session);
    }

    public void MarkCountdownNotified()
    {
        var session = RequireActive();
        session.CountdownCompletedNotified = true;
        _store.UpdateSession(session);
    }

    public TimeSpan GetActiveDuration(Guid? sessionId = null, DateTimeOffset? now = null)
    {
        var id = sessionId ?? ActiveSession?.Id;
        if (id is null)
        {
            return TimeSpan.Zero;
        }

        return _store.GetActiveDuration(id.Value, now ?? _clock());
    }

    public IReadOnlyList<WorkSession> GetTodaysSessions(DateTimeOffset? day = null)
    {
        return _store.GetSessionsForLocalDay(day ?? _clock());
    }

    public TimeSpan GetTodaysTotal(DateTimeOffset? day = null)
    {
        var sessions = GetTodaysSessions(day);
        var total = TimeSpan.Zero;
        var now = day ?? _clock();

        foreach (var session in sessions)
        {
            if (session.State == SessionState.Cancelled)
            {
                continue;
            }

            total += _store.GetActiveDuration(session.Id, now);
        }

        return total;
    }

    public void UpdateSessionDetails(WorkSession session, IReadOnlyList<WorkInterval> intervals)
    {
        _store.UpdateSessionDetails(session, intervals);
        RaiseChanged();
    }

    public void DeleteSession(Guid sessionId)
    {
        if (ActiveSession?.Id == sessionId)
        {
            throw new InvalidOperationException("Cannot delete the active session. Finish or discard it first.");
        }

        _store.DeleteSession(sessionId);
        RaiseChanged();
    }

    public IReadOnlyList<WorkInterval> GetIntervals(Guid sessionId) => _store.GetIntervals(sessionId);

    private WorkSession RequireActive()
    {
        return _store.GetActiveSession()
            ?? throw new InvalidOperationException("No active work session.");
    }

    private void RaiseChanged() => SessionChanged?.Invoke(this, EventArgs.Empty);
}
