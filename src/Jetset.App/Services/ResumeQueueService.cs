using Jetset.App.Models;

namespace Jetset.App.Services;

/// <summary>
/// Derives the ordered resume queue from active tasks and paused in-progress sessions.
/// </summary>
public sealed class ResumeQueueService
{
    private readonly TaskService _tasks;
    private readonly SessionService _sessions;

    public ResumeQueueService(TaskService tasks, SessionService sessions)
    {
        _tasks = tasks;
        _sessions = sessions;
    }

    public IReadOnlyList<ResumeQueueEntry> GetOrderedTasks()
    {
        var focusedSessionId = _sessions.ActiveSession?.Id;
        var entries = new List<ResumeQueueEntry>();

        foreach (var session in _sessions.GetInProgressSessions())
        {
            if (session.State != SessionState.Paused || session.Id == focusedSessionId)
            {
                continue;
            }

            var task = _tasks.Get(session.TaskId);
            if (task is null || !_tasks.IsEligibleForActiveWork(task))
            {
                continue;
            }

            entries.Add(new ResumeQueueEntry
            {
                Task = task,
                PausedSession = session
            });
        }

        return entries
            .OrderByDescending(e => e.Task.LastWorkedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(e => e.Task.UpdatedAt)
            .ThenBy(e => e.Task.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
