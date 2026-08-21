using Jetset.App.Models;

namespace Jetset.App.Persistence;

public interface ITaskStore
{
    WorkTask? Get(Guid id);

    IReadOnlyList<WorkTask> List();

    IReadOnlyList<WorkTask> ListByProject(Guid? projectId);

    IReadOnlyList<WorkTask> Search(string query);

    int CountByProject(Guid projectId);

    IReadOnlyList<WorkTask> ListByMilestone(Guid milestoneId);

    int CountByMilestone(Guid milestoneId);

    int CountDoneByMilestone(Guid milestoneId);

    void UnassignAllFromProject(Guid projectId);

    void UnassignAllFromMilestone(Guid milestoneId);

    void Insert(WorkTask task);

    void Update(WorkTask task);

    void Delete(Guid id);
}
