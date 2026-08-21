using Jetset.App.Models;

namespace Jetset.App.Persistence;

public sealed class InMemoryTaskSwitchEventStore : ITaskSwitchEventStore
{
    private readonly List<TaskSwitchEvent> _events = [];

    public void Insert(TaskSwitchEvent switchEvent) => _events.Add(Clone(switchEvent));

    public IReadOnlyList<TaskSwitchEvent> ListBetween(DateTimeOffset startInclusive, DateTimeOffset endExclusive) =>
        _events
            .Where(e => e.OccurredAt >= startInclusive && e.OccurredAt < endExclusive)
            .OrderBy(e => e.OccurredAt)
            .Select(Clone)
            .ToList();

    private static TaskSwitchEvent Clone(TaskSwitchEvent switchEvent) =>
        new()
        {
            Id = switchEvent.Id,
            FromTaskId = switchEvent.FromTaskId,
            ToTaskId = switchEvent.ToTaskId,
            OccurredAt = switchEvent.OccurredAt
        };
}
