namespace Jetset.App.Models;

public static class TaskStatusRules
{
    public static bool IsTerminal(TaskStatus status) =>
        status is TaskStatus.Done or TaskStatus.Cancelled;

    public static bool IsEligibleForActiveWork(TaskStatus status) =>
        status is TaskStatus.Active or TaskStatus.Blocked;

    public static bool CanTransition(TaskStatus from, TaskStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return (from, to) switch
        {
            (TaskStatus.Active, TaskStatus.Blocked) => true,
            (TaskStatus.Blocked, TaskStatus.Active) => true,
            (TaskStatus.Active, TaskStatus.Done) => true,
            (TaskStatus.Active, TaskStatus.Cancelled) => true,
            (TaskStatus.Blocked, TaskStatus.Done) => true,
            (TaskStatus.Blocked, TaskStatus.Cancelled) => true,
            (TaskStatus.Done, TaskStatus.Active) => true,
            (TaskStatus.Cancelled, TaskStatus.Active) => true,
            _ => false
        };
    }
}
