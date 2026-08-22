namespace Jetset.App.Models;

public static class TaskStatusRules
{
    public static bool IsTerminal(TaskStatus status) =>
        status is TaskStatus.Done or TaskStatus.Cancelled;

    public static bool IsRunning(TaskStatus status) =>
        status == TaskStatus.Running;

    public static bool IsEligibleForActiveWork(TaskStatus status) =>
        status is TaskStatus.Ready or TaskStatus.Waiting or TaskStatus.Inbox;

    public static bool CanStart(TaskStatus status) =>
        IsEligibleForActiveWork(status);

    public static bool CanTransition(TaskStatus from, TaskStatus to)
    {
        if (from == to)
        {
            return true;
        }

        if (to == TaskStatus.Running)
        {
            return false;
        }

        return (from, to) switch
        {
            (TaskStatus.Inbox, TaskStatus.Ready) => true,
            (TaskStatus.Inbox, TaskStatus.Waiting) => true,
            (TaskStatus.Inbox, TaskStatus.Done) => true,
            (TaskStatus.Inbox, TaskStatus.Cancelled) => true,

            (TaskStatus.Ready, TaskStatus.Inbox) => true,
            (TaskStatus.Ready, TaskStatus.Waiting) => true,
            (TaskStatus.Ready, TaskStatus.Done) => true,
            (TaskStatus.Ready, TaskStatus.Cancelled) => true,

            (TaskStatus.Running, TaskStatus.Ready) => true,
            (TaskStatus.Running, TaskStatus.Waiting) => true,
            (TaskStatus.Running, TaskStatus.Done) => true,
            (TaskStatus.Running, TaskStatus.Cancelled) => true,

            (TaskStatus.Waiting, TaskStatus.Inbox) => true,
            (TaskStatus.Waiting, TaskStatus.Ready) => true,
            (TaskStatus.Waiting, TaskStatus.Done) => true,
            (TaskStatus.Waiting, TaskStatus.Cancelled) => true,

            (TaskStatus.Done, TaskStatus.Ready) => true,
            (TaskStatus.Done, TaskStatus.Inbox) => true,

            (TaskStatus.Cancelled, TaskStatus.Ready) => true,
            (TaskStatus.Cancelled, TaskStatus.Inbox) => true,

            _ => false
        };
    }
}
