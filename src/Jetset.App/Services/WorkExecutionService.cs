using Jetset.App.Models;

namespace Jetset.App.Services;

/// <summary>
/// Orchestrates task selection and session start/resume while keeping task work metadata current.
/// </summary>
public sealed class WorkExecutionService
{
    private readonly SessionService _sessions;
    private readonly TaskService _tasks;
    private readonly ContextSnapshotService _snapshots;

    public WorkExecutionService(
        SessionService sessions,
        TaskService tasks,
        ContextSnapshotService snapshots)
    {
        _sessions = sessions;
        _tasks = tasks;
        _snapshots = snapshots;
    }

    public WorkSession? GetInProgressSessionForTask(Guid taskId) =>
        _sessions.GetInProgressSessions().FirstOrDefault(s => s.TaskId == taskId);

    public WorkTask? GetActiveTask()
    {
        var session = _sessions.ActiveSession;
        return session is null ? null : _tasks.Get(session.TaskId);
    }

    public WorkTask? GetLeavingTask()
    {
        var running = GetRunningSession();
        return running is null ? null : _tasks.Get(running.TaskId);
    }

    public bool IsTaskFocused(Guid taskId) =>
        _sessions.ActiveSession?.TaskId == taskId;

    public bool HasPausedSession(Guid taskId) =>
        GetInProgressSessionForTask(taskId)?.State == SessionState.Paused;

    public WorkSession StartWork(
        Guid taskId,
        TimerMode mode = TimerMode.Stopwatch,
        TimeSpan? countdownDuration = null,
        WorkingContext? leavingContext = null)
    {
        var task = RequireEligibleTask(taskId);

        var existing = GetInProgressSessionForTask(taskId);
        if (existing is not null)
        {
            if (existing.State == SessionState.Paused)
            {
                TouchLeavingRunningTask();
                PreserveLeavingRunningTask(leavingContext, exceptTaskId: taskId);
                _sessions.SwitchTo(existing.Id);
            }

            _tasks.RecordWorkStarted(taskId);
            return _sessions.ActiveSession
                ?? throw new InvalidOperationException("Session was not activated.");
        }

        TouchLeavingRunningTask();
        PreserveLeavingRunningTask(leavingContext, exceptTaskId: taskId);
        var session = _sessions.Start(task.Id, mode, countdownDuration);
        _tasks.RecordWorkStarted(taskId);
        return session;
    }

    public WorkSession ResumeWork(Guid taskId, WorkingContext? leavingContext = null)
    {
        RequireEligibleTask(taskId);

        var existing = GetInProgressSessionForTask(taskId);
        if (existing is not null)
        {
            PreserveLeavingRunningTask(leavingContext, exceptTaskId: taskId);
            _sessions.SwitchTo(existing.Id);
            _tasks.RecordWorkStarted(taskId);
            return _sessions.ActiveSession
                ?? throw new InvalidOperationException("Session was not activated.");
        }

        return StartWork(taskId, leavingContext: leavingContext);
    }

    public void SwitchToSession(Guid sessionId, WorkingContext? leavingContext = null)
    {
        var session = _sessions.GetInProgressSessions().FirstOrDefault(s => s.Id == sessionId)
            ?? throw new InvalidOperationException("Session is not in progress.");

        TouchLeavingRunningTask();
        PreserveLeavingRunningTask(leavingContext, exceptTaskId: session.TaskId);
        _sessions.SwitchTo(sessionId);
        _tasks.RecordWorkStarted(session.TaskId);
    }

    public void PauseWork(WorkingContext? contextUpdate = null)
    {
        var session = _sessions.ActiveSession
            ?? throw new InvalidOperationException("No active work session.");

        if (session.State != SessionState.Running)
        {
            throw new InvalidOperationException("Only a running session can be paused.");
        }

        PreserveContext(session.TaskId, contextUpdate);
        _sessions.Pause();
        _tasks.RecordWorkStarted(session.TaskId);
    }

    public WorkSession FinishWork(
        string? note = null,
        WorkingContext? contextUpdate = null,
        DateTimeOffset? finishedAt = null)
    {
        var session = _sessions.ActiveSession
            ?? throw new InvalidOperationException("No active work session.");

        PreserveContext(session.TaskId, contextUpdate);
        return _sessions.Finish(note, finishedAt);
    }

    public WorkSession FinishAtLastKnownActivity(WorkingContext? contextUpdate = null)
    {
        var session = _sessions.ActiveSession
            ?? throw new InvalidOperationException("No active work session.");

        PreserveContext(session.TaskId, contextUpdate);
        return _sessions.FinishAtLastKnownActivity();
    }

    private WorkTask RequireEligibleTask(Guid taskId)
    {
        var task = _tasks.Get(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} was not found.");

        if (!_tasks.IsEligibleForActiveWork(task))
        {
            throw new InvalidOperationException(
                $"Task \"{task.Title}\" is not eligible for active work.");
        }

        return task;
    }

    private WorkSession? GetRunningSession() =>
        _sessions.GetInProgressSessions().FirstOrDefault(s => s.State == SessionState.Running);

    private void TouchLeavingRunningTask()
    {
        var running = GetRunningSession();
        if (running is not null)
        {
            _tasks.RecordWorkStarted(running.TaskId);
        }
    }

    private void PreserveLeavingRunningTask(WorkingContext? contextUpdate, Guid? exceptTaskId)
    {
        var running = GetRunningSession();
        if (running is null || running.TaskId == exceptTaskId)
        {
            return;
        }

        PreserveContext(running.TaskId, contextUpdate);
    }

    private void PreserveContext(Guid taskId, WorkingContext? contextUpdate)
    {
        if (contextUpdate is not null)
        {
            _tasks.UpdateContext(
                taskId,
                contextUpdate.CurrentStatus,
                contextUpdate.LastProgress,
                contextUpdate.NextAction,
                contextUpdate.Blocker,
                contextUpdate.Notes);
        }

        _snapshots.Capture(taskId);
    }
}
