using Jetset.App.Models;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.Services;

/// <summary>
/// Coordinates task execution state with session start/pause/finish.
/// <see cref="TaskService"/> is the authority for Running tasks; sessions follow.
/// </summary>
public sealed class WorkExecutionService
{
    private readonly SessionService _sessions;
    private readonly TaskService _tasks;

    public WorkExecutionService(SessionService sessions, TaskService tasks)
    {
        _sessions = sessions;
        _tasks = tasks;
    }

    public WorkSession? GetInProgressSessionForTask(Guid taskId) =>
        _sessions.GetInProgressSessions().FirstOrDefault(s => s.TaskId == taskId);

    public WorkTask? GetActiveTask() => _tasks.GetRunningTask();

    public WorkTask? GetLeavingTask()
    {
        var running = _sessions.GetInProgressSessions()
            .FirstOrDefault(s => s.State == SessionState.Running);
        return running is null ? null : _tasks.Get(running.TaskId);
    }

    public bool IsTaskFocused(Guid taskId) =>
        _tasks.GetRunningTask()?.Id == taskId &&
        _sessions.ActiveSession?.TaskId == taskId;

    public bool HasPausedSession(Guid taskId) =>
        GetInProgressSessionForTask(taskId)?.State == SessionState.Paused;

    public WorkSession StartWork(
        Guid taskId,
        TimerMode mode = TimerMode.Stopwatch,
        TimeSpan? countdownDuration = null,
        TaskStatus leavingStatus = TaskStatus.Ready)
    {
        _tasks.StartTask(taskId, leavingStatus);
        return ActivateSessionForTask(taskId, mode, countdownDuration);
    }

    public WorkSession ResumeWork(
        Guid taskId,
        TaskStatus leavingStatus = TaskStatus.Ready)
    {
        var existing = GetInProgressSessionForTask(taskId);
        if (existing is { State: SessionState.Paused }
            && _tasks.GetRunningTask()?.Id == taskId)
        {
            _sessions.SwitchTo(existing.Id);
            return _sessions.ActiveSession
                ?? throw new InvalidOperationException("Session was not activated.");
        }

        return StartWork(taskId, leavingStatus: leavingStatus);
    }

    public void SwitchToSession(Guid sessionId, TaskStatus leavingStatus = TaskStatus.Ready)
    {
        var session = _sessions.GetInProgressSessions().FirstOrDefault(s => s.Id == sessionId)
            ?? throw new InvalidOperationException("Session is not in progress.");

        _tasks.StartTask(session.TaskId, leavingStatus);
        _sessions.SwitchTo(sessionId);
    }

    public void PauseWork()
    {
        var session = _sessions.ActiveSession
            ?? throw new InvalidOperationException("No active work session.");

        if (session.State != SessionState.Running)
        {
            throw new InvalidOperationException("Only a running session can be paused.");
        }

        _sessions.Pause();
    }

    public WorkSession FinishWork(string? note = null, DateTimeOffset? finishedAt = null)
    {
        var session = _sessions.ActiveSession
            ?? throw new InvalidOperationException("No active work session.");

        var taskId = session.TaskId;
        var finished = _sessions.Finish(note, finishedAt);

        if (_tasks.GetRunningTask()?.Id == taskId)
        {
            _tasks.StopTask(taskId);
        }

        return finished;
    }

    public WorkSession FinishAtLastKnownActivity()
    {
        var session = _sessions.ActiveSession
            ?? throw new InvalidOperationException("No active work session.");

        var finishAt = session.LastHeartbeatAt
            ?? _sessions.GetIntervals(session.Id).LastOrDefault()?.EndedAt
            ?? _sessions.GetIntervals(session.Id).LastOrDefault()?.StartedAt
            ?? session.StartedAt;

        return FinishWork(finishedAt: finishAt);
    }

    private WorkSession ActivateSessionForTask(
        Guid taskId,
        TimerMode mode,
        TimeSpan? countdownDuration)
    {
        var existing = GetInProgressSessionForTask(taskId);
        if (existing is not null)
        {
            if (existing.State == SessionState.Paused)
            {
                _sessions.SwitchTo(existing.Id);
            }

            return _sessions.ActiveSession
                ?? throw new InvalidOperationException("Session was not activated.");
        }

        return _sessions.Start(taskId, mode, countdownDuration);
    }
}
