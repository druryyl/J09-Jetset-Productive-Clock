namespace Jetset.App.Persistence;

public sealed class InMemoryTreeStateStore : ITreeStateStore
{
    private readonly HashSet<Guid> _expanded = [];

    public IReadOnlySet<Guid> GetExpandedProjectIds() => _expanded;

    public bool IsExpanded(Guid projectId) => _expanded.Contains(projectId);

    public void SetExpanded(Guid projectId, bool expanded)
    {
        if (expanded)
            _expanded.Add(projectId);
        else
            _expanded.Remove(projectId);
    }
}
