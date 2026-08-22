using Jetset.App.Models;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.Services;

/// <summary>
/// Task ↔ Project conversion per ADR-0007 Decisions 2–3.
/// </summary>
public sealed class WorkItemConversionService
{
    private readonly TaskService _tasks;
    private readonly ProjectService _projects;

    public WorkItemConversionService(TaskService tasks, ProjectService projects)
    {
        _tasks = tasks;
        _projects = projects;
    }

    public bool CanConvertTaskToProject(Guid taskId)
    {
        var task = _tasks.Get(taskId);
        return task is not null && task.Status != TaskStatus.Running;
    }

    public bool CanConvertProjectToTask(Guid projectId)
    {
        var project = _projects.Get(projectId);
        return project is not null && _tasks.ListByProject(projectId).Count == 0;
    }

    public ProjectToTaskConversionInfo GetProjectToTaskInfo(Guid projectId)
    {
        var project = _projects.Get(projectId)
            ?? throw new InvalidOperationException($"Project {projectId} was not found.");

        return new ProjectToTaskConversionInfo(
            ProjectId: project.Id,
            ProjectName: project.Name,
            HasChildren: _tasks.ListByProject(projectId).Count > 0,
            HasDeadline: project.Deadline is not null,
            HasContext: !string.IsNullOrWhiteSpace(project.ContextText));
    }

    public Project ConvertTaskToProject(Guid taskId)
    {
        var task = _tasks.Get(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} was not found.");

        if (task.Status == TaskStatus.Running)
        {
            throw new InvalidOperationException(
                $"Task \"{task.Title}\" cannot be converted while Running.");
        }

        var project = _projects.Create(task.Title);
        _tasks.Delete(taskId);
        return project;
    }

    public WorkTask ConvertProjectToTask(Guid projectId, bool transferContextToNotes = false)
    {
        var project = _projects.Get(projectId)
            ?? throw new InvalidOperationException($"Project {projectId} was not found.");

        if (_tasks.ListByProject(projectId).Count > 0)
        {
            throw new InvalidOperationException(
                $"Project \"{project.Name}\" has child tasks and cannot be converted.");
        }

        var task = _tasks.Create(project.Name);
        if (transferContextToNotes && !string.IsNullOrWhiteSpace(project.ContextText))
        {
            task.Notes = project.ContextText.Trim();
            task = _tasks.Update(task);
        }

        if (task.Status != TaskStatus.Ready)
        {
            task = _tasks.ChangeStatus(task.Id, TaskStatus.Ready);
        }

        _projects.Delete(projectId);
        return task;
    }
}

public sealed record ProjectToTaskConversionInfo(
    Guid ProjectId,
    string ProjectName,
    bool HasChildren,
    bool HasDeadline,
    bool HasContext);
