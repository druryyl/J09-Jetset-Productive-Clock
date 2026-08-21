using Jetset.App.Models;

namespace Jetset.App.Persistence;

public interface IContextSnapshotStore
{
    void Insert(ContextSnapshot snapshot);

    IReadOnlyList<ContextSnapshot> ListByTask(Guid taskId);

    ContextSnapshot? GetLatest(Guid taskId);

    void DeleteByTask(Guid taskId);
}
