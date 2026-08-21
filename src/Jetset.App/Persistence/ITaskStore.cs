using Jetset.App.Models;

namespace Jetset.App.Persistence;

public interface ITaskStore
{
    WorkTask? Get(Guid id);

    IReadOnlyList<WorkTask> List();

    IReadOnlyList<WorkTask> ListByProject(Guid? projectId);

    IReadOnlyList<WorkTask> Search(string query);

    int CountByProject(Guid projectId);

    void UnassignAllFromProject(Guid projectId);

    void Insert(WorkTask task);

    void Update(WorkTask task);

    void Delete(Guid id);
}
