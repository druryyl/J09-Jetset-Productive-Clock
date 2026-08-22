using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class WorkItemConversionServiceTests
{
    private static (
        WorkItemConversionService Conversion,
        TaskService Tasks,
        ProjectService Projects,
        InMemoryTaskStore TaskStore,
        InMemoryProjectStore ProjectStore,
        Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var taskStore = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => taskStore.List());
        var tasks = new TaskService(taskStore, projectStore, () => now);
        var projects = new ProjectService(projectStore, taskStore, () => now);
        var conversion = new WorkItemConversionService(tasks, projects);
        return (conversion, tasks, projects, taskStore, projectStore, value => now = value);
    }

    [Fact]
    public void ConvertTaskToProject_CreatesProjectAndRemovesTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, projects, taskStore, projectStore, _) = CreateHarness(start);

        var task = tasks.Create("Implement Jetset V2");
        var project = conversion.ConvertTaskToProject(task.Id);

        Assert.Equal("Implement Jetset V2", project.Name);
        Assert.NotNull(projects.Get(project.Id));
        Assert.Null(tasks.Get(task.Id));
        Assert.Null(taskStore.Get(task.Id));
        Assert.Single(projectStore.List());
    }

    [Fact]
    public void ConvertTaskToProject_WhenRunning_Throws()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, _, _, projectStore, _) = CreateHarness(start);

        var task = tasks.Create("Active work");
        tasks.StartTask(task.Id);

        var ex = Assert.Throws<InvalidOperationException>(
            () => conversion.ConvertTaskToProject(task.Id));

        Assert.Contains("Running", ex.Message);
        Assert.Empty(projectStore.List());
        Assert.NotNull(tasks.Get(task.Id));
    }

    [Fact]
    public void ConvertTaskToProject_WhenTaskNotFound_Throws()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, _, _, _, _, _) = CreateHarness(start);

        Assert.Throws<InvalidOperationException>(
            () => conversion.ConvertTaskToProject(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(TaskStatus.Inbox)]
    [InlineData(TaskStatus.Ready)]
    [InlineData(TaskStatus.Waiting)]
    [InlineData(TaskStatus.Done)]
    [InlineData(TaskStatus.Cancelled)]
    public void ConvertTaskToProject_AllowsNonRunningStatuses(TaskStatus status)
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, _, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Convertible");
        if (status != TaskStatus.Inbox)
        {
            tasks.ChangeStatus(task.Id, status);
        }

        var project = conversion.ConvertTaskToProject(task.Id);

        Assert.Equal("Convertible", project.Name);
        Assert.Null(tasks.Get(task.Id));
    }

    [Fact]
    public void ConvertProjectToTask_CreatesReadyRootTaskAndRemovesProject()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, projects, _, projectStore, _) = CreateHarness(start);

        var project = projects.Create("Empty project");
        var task = conversion.ConvertProjectToTask(project.Id);

        Assert.Equal("Empty project", task.Title);
        Assert.Equal(TaskStatus.Ready, task.Status);
        Assert.Null(task.ProjectId);
        Assert.NotNull(tasks.Get(task.Id));
        Assert.Null(projects.Get(project.Id));
        Assert.Empty(projectStore.List());
    }

    [Fact]
    public void ConvertProjectToTask_WhenChildrenExist_Throws()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, projects, _, _, _) = CreateHarness(start);

        var project = projects.Create("Parent");
        tasks.Create("Child", project.Id);

        var ex = Assert.Throws<InvalidOperationException>(
            () => conversion.ConvertProjectToTask(project.Id));

        Assert.Contains("child tasks", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(projects.Get(project.Id));
    }

    [Fact]
    public void ConvertProjectToTask_WhenProjectNotFound_Throws()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, _, _, _, _, _) = CreateHarness(start);

        Assert.Throws<InvalidOperationException>(
            () => conversion.ConvertProjectToTask(Guid.NewGuid()));
    }

    [Fact]
    public void ConvertProjectToTask_TransfersContextWhenRequested()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, projects, _, _, _) = CreateHarness(start);

        var project = projects.Create("With context");
        projects.UpdateContextText(project.Id, "  Keep this context  ");

        var task = conversion.ConvertProjectToTask(project.Id, transferContextToNotes: true);

        Assert.Equal("Keep this context", task.Notes);
        Assert.Null(projects.Get(project.Id));
    }

    [Fact]
    public void ConvertProjectToTask_DoesNotTransferContextWhenNotRequested()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, projects, _, _, _) = CreateHarness(start);

        var project = projects.Create("With context");
        projects.UpdateContextText(project.Id, "Discard me");

        var task = conversion.ConvertProjectToTask(project.Id, transferContextToNotes: false);

        Assert.Null(task.Notes);
    }

    [Fact]
    public void CanConvertTaskToProject_ReturnsFalseForRunningTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, _, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Running");
        tasks.StartTask(task.Id);

        Assert.False(conversion.CanConvertTaskToProject(task.Id));
    }

    [Fact]
    public void CanConvertTaskToProject_ReturnsTrueForReadyTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, _, _, _, _) = CreateHarness(start);

        var task = tasks.Create("Ready");
        tasks.ChangeStatus(task.Id, TaskStatus.Ready);

        Assert.True(conversion.CanConvertTaskToProject(task.Id));
    }

    [Fact]
    public void CanConvertProjectToTask_ReturnsFalseWhenChildrenExist()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, projects, _, _, _) = CreateHarness(start);

        var project = projects.Create("Parent");
        tasks.Create("Child", project.Id);

        Assert.False(conversion.CanConvertProjectToTask(project.Id));
    }

    [Fact]
    public void CanConvertProjectToTask_ReturnsTrueForEmptyProject()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, _, projects, _, _, _) = CreateHarness(start);

        var project = projects.Create("Empty");

        Assert.True(conversion.CanConvertProjectToTask(project.Id));
    }

    [Fact]
    public void GetProjectToTaskInfo_ReportsDeadlineAndContext()
    {
        var start = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
        var (conversion, tasks, projects, _, _, _) = CreateHarness(start);

        var project = projects.Create("Info test", new DateOnly(2026, 12, 31));
        projects.UpdateContextText(project.Id, "Context here");
        tasks.Create("Child", project.Id);

        var info = conversion.GetProjectToTaskInfo(project.Id);

        Assert.Equal(project.Id, info.ProjectId);
        Assert.Equal("Info test", info.ProjectName);
        Assert.True(info.HasChildren);
        Assert.True(info.HasDeadline);
        Assert.True(info.HasContext);
    }
}
