using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

public sealed class SessionService
{
    private readonly ISessionStore _store;
    private readonly ITaskStore _taskStore;
    private readonly Func<DateTimeOffset> _clock;

    public SessionService(
        ISessionStore store,
        ITaskStore taskStore,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _taskStore = taskStore;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public WorkSession? ActiveSession => _store.GetActiveSession();

    public bool HasActiveSession => ActiveSession is not null;

    public event EventHandler? SessionChanged;

    public IReadOnlyList<WorkSession> GetInProgressSessions() => _store.GetInProgressSessions();

    public WorkSession Start(Guid taskId, TimerMode mode, TimeSpan? countdownDuration)
    {
        var task = _taskStore.Get(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} was not found.");

        return StartInternal(task.Id, task.Title, mode, countdownDuration);
    }

    private WorkSession StartInternal(Guid taskId, string taskTitle, TimerMode mode, TimeSpan? countdownDuration)
    {
        var name = taskTitle.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task title is required.", nameof(taskTitle));
        }

        if (mode == TimerMode.Countdown)
        {
            if (countdownDuration is null || countdownDuration <= TimeSpan.Zero)
            {
                throw new ArgumentException("Countdown duration must be greater than zero.", nameof(countdownDuration));
            }
        }

        var running = GetRunningSession();

        if (running is not null)
        {
            PauseSession(running);
        }

        var now = _clock();
        var session = new WorkSession
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
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
        PauseSession(RequireActive());
        RaiseChanged();
    }

    public void Resume()
    {
        ResumeSession(RequireActive());
        RaiseChanged();
    }

    public void SwitchTo(Guid sessionId)
    {
        var target = GetInProgressSessions().FirstOrDefault(s => s.Id == sessionId)
            ?? throw new InvalidOperationException("Session is not in progress.");

        var running = GetRunningSession();
        if (running is not null && running.Id == sessionId)
        {
            return;
        }

        if (running is not null)
        {
            PauseSession(running);
        }

        // Reload target after possible pause of another session.
        target = _store.GetInProgressSessions().First(s => s.Id == sessionId);
        if (target.State == SessionState.Paused)
        {
            ResumeSession(target);
        }

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

        PromoteNextFocused(now, excludeSessionId: session.Id);
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

        PromoteNextFocused(now, excludeSessionId: session.Id);
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

    public IReadOnlyList<WorkSession> GetSessionsByTaskId(Guid taskId) =>
        _store.GetSessionsByTaskId(taskId);

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
        var inProgress = _store.GetInProgressSessions();
        if (inProgress.Any(s => s.Id == sessionId))
        {
            throw new InvalidOperationException("Cannot delete an in-progress session. Finish or discard it first.");
        }

        _store.DeleteSession(sessionId);
        RaiseChanged();
    }

    public IReadOnlyList<WorkInterval> GetIntervals(Guid sessionId) => _store.GetIntervals(sessionId);

    private WorkSession? GetRunningSession()
    {
        return _store.GetInProgressSessions().FirstOrDefault(s => s.State == SessionState.Running);
    }

    private void PauseSession(WorkSession session)
    {
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
    }

    private void ResumeSession(WorkSession session)
    {
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

        if (GetRunningSession() is not null)
        {
            throw new InvalidOperationException("Only one running work session is allowed.");
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
    }

    private void PromoteNextFocused(DateTimeOffset now, Guid excludeSessionId)
    {
        var next = _store.GetInProgressSessions()
            .FirstOrDefault(s => s.Id != excludeSessionId && s.State == SessionState.Paused);
        if (next is null)
        {
            return;
        }

        next.LastHeartbeatAt = now;
        _store.UpdateSession(next);
    }

    private WorkSession RequireActive()
    {
        return _store.GetActiveSession()
            ?? throw new InvalidOperationException("No active work session.");
    }

    private void RaiseChanged() => SessionChanged?.Invoke(this, EventArgs.Empty);
}
