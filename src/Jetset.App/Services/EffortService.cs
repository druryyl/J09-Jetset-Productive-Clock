using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

/// <summary>
/// Derived effort calculations for tasks and project rollups.
/// </summary>
public sealed class EffortService
{
    private readonly SessionService _sessions;
    private readonly ITaskStore _taskStore;

    public EffortService(SessionService sessions, ITaskStore taskStore)
    {
        _sessions = sessions;
        _taskStore = taskStore;
    }

    public TimeSpan GetTaskSpent(Guid taskId)
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

    public ProjectEffortRollup GetProjectRollup(Guid projectId)
    {
        var tasks = _taskStore.ListByProject(projectId);
        var spent = TimeSpan.Zero;
        int? estimateMinutes = null;

        foreach (var task in tasks)
        {
            spent += GetTaskSpent(task.Id);

            if (task.EstimateMinutes is int minutes)
            {
                estimateMinutes = (estimateMinutes ?? 0) + minutes;
            }
        }

        return new ProjectEffortRollup
        {
            Spent = spent,
            EstimateMinutes = estimateMinutes
        };
    }
}
