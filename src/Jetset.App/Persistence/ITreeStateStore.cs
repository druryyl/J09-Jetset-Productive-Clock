namespace Jetset.App.Persistence;

/// <summary>
/// UI-only persistence for work tree expand/collapse state (ADR Decision 6).
/// </summary>
public interface ITreeStateStore
{
    IReadOnlySet<Guid> GetExpandedProjectIds();

    bool IsExpanded(Guid projectId);

    void SetExpanded(Guid projectId, bool expanded);
}
