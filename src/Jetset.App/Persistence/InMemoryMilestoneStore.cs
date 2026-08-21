using Jetset.App.Models;

namespace Jetset.App.Persistence;

public sealed class InMemoryMilestoneStore : IMilestoneStore
{
    private readonly Dictionary<Guid, Milestone> _milestones = new();

    public Milestone? Get(Guid id) =>
        _milestones.TryGetValue(id, out var milestone) ? Clone(milestone) : null;

    public IReadOnlyList<Milestone> ListByProject(Guid projectId) =>
        _milestones.Values
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.SortOrder)
            .Select(Clone)
            .ToList();

    public void Insert(Milestone milestone) => _milestones[milestone.Id] = Clone(milestone);

    public void Update(Milestone milestone)
    {
        if (!_milestones.ContainsKey(milestone.Id))
        {
            throw new InvalidOperationException($"Milestone {milestone.Id} was not found.");
        }

        _milestones[milestone.Id] = Clone(milestone);
    }

    public void Delete(Guid id) => _milestones.Remove(id);

    public void DeleteByProject(Guid projectId)
    {
        foreach (var id in _milestones.Values.Where(m => m.ProjectId == projectId).Select(m => m.Id).ToList())
        {
            _milestones.Remove(id);
        }
    }

    public void UpdateSortOrders(Guid projectId, IReadOnlyList<Guid> orderedIds)
    {
        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (!_milestones.TryGetValue(orderedIds[i], out var existing) || existing.ProjectId != projectId)
            {
                throw new InvalidOperationException(
                    $"Milestone {orderedIds[i]} was not found in project {projectId}.");
            }

            _milestones[orderedIds[i]] = Clone(new Milestone
            {
                Id = existing.Id,
                ProjectId = existing.ProjectId,
                Name = existing.Name,
                SortOrder = i,
                CreatedAt = existing.CreatedAt
            });
        }
    }

    private static Milestone Clone(Milestone m) => new()
    {
        Id = m.Id,
        ProjectId = m.ProjectId,
        Name = m.Name,
        SortOrder = m.SortOrder,
        CreatedAt = m.CreatedAt
    };
}
