using Jetset.App.Models;

namespace Jetset.App.Persistence;

public interface ITaskSwitchEventStore
{
    void Insert(TaskSwitchEvent switchEvent);

    IReadOnlyList<TaskSwitchEvent> ListBetween(DateTimeOffset startInclusive, DateTimeOffset endExclusive);
}
