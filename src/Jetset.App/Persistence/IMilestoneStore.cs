using Jetset.App.Models;

namespace Jetset.App.Persistence;

public interface IMilestoneStore
{
    Milestone? Get(Guid id);

    IReadOnlyList<Milestone> ListByProject(Guid projectId);

    void Insert(Milestone milestone);

    void Update(Milestone milestone);

    void Delete(Guid id);

    void DeleteByProject(Guid projectId);

    void UpdateSortOrders(Guid projectId, IReadOnlyList<Guid> orderedIds);
}
