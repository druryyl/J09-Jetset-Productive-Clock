using Jetset.App.Models;

namespace Jetset.App.Persistence;

public interface IProjectStore
{
    Project? Get(Guid id);

    IReadOnlyList<Project> List();

    IReadOnlyList<ProjectSummary> ListWithTaskCounts();

    void Insert(Project project);

    void Update(Project project);

    void Delete(Guid id);
}
