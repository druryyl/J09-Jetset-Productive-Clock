using System.IO;
using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Persistence.Migrations;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class TaskServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static TaskServiceTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public TaskServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetTaskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var path in _dbPaths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup of temp DB files.
            }
        }

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of temp directory.
        }
    }

    private static (TaskService Service, InMemoryTaskStore Store, Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var (service, store, _, setNow) = CreateHarnessWithProjects(start);
        return (service, store, setNow);
    }

    private static (TaskService Service, InMemoryTaskStore Store, InMemoryProjectStore ProjectStore, Action<DateTimeOffset> SetNow)
        CreateHarnessWithProjects(DateTimeOffset start)
    {
        var now = start;
        var store = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => store.List());
        var service = new TaskService(store, projectStore, () => now);
        return (service, store, projectStore, value => now = value);
    }

    private static Project InsertProject(InMemoryProjectStore projectStore, string name, DateTimeOffset timestamp)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
        projectStore.Insert(project);
        return project;
    }

    [Fact]
    public void Create_WithTitle_PersistsInboxTaskWithUnplannedOrigin()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, store, _) = CreateHarness(start);

        var task = service.Create("Review PR");

        Assert.Equal("Review PR", task.Title);
        Assert.Equal(TaskStatus.Inbox, task.Status);
        Assert.Equal(TaskOrigin.Unplanned, task.Origin);
        Assert.Equal(start, task.CreatedAt);
        Assert.Equal(start, task.UpdatedAt);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.ProjectId);

        var loaded = store.Get(task.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Review PR", loaded!.Title);
    }

    [Fact]
    public void Create_WithBlankTitle_Throws()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => service.Create("   "));
        Assert.Throws<ArgumentException>(() => service.Create(""));
    }

    [Fact]
    public void Update_ChangesTitleAndNotesWithoutChangingStatus()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);

        var task = service.Create("Original");
        setNow(start.AddMinutes(5));

        task.Title = "Updated title";
        task.Notes = "Some notes";
        task.Status = TaskStatus.Waiting;

        var updated = service.Update(task);

        Assert.Equal("Updated title", updated.Title);
        Assert.Equal("Some notes", updated.Notes);
        Assert.Equal(TaskStatus.Inbox, updated.Status);
        Assert.Equal(start, updated.CreatedAt);
        Assert.Equal(start.AddMinutes(5), updated.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ReadyToWaiting_Succeeds()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);
        var task = service.Create("Block me");
        setNow(start.AddMinutes(1));

        var updated = service.ChangeStatus(task.Id, TaskStatus.Waiting);

        Assert.Equal(TaskStatus.Waiting, updated.Status);
        Assert.Equal(start.AddMinutes(1), updated.UpdatedAt);
        Assert.Equal(TaskStatus.Waiting, service.Get(task.Id)!.Status);
    }

    [Fact]
    public void ChangeStatus_DoneToReady_ReopensAndClearsCompletedAt()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);
        var task = service.Create("Done task");
        setNow(start.AddMinutes(1));
        service.CompleteTask(task.Id);

        var completed = service.Get(task.Id)!;
        Assert.NotNull(completed.CompletedAt);

        setNow(start.AddMinutes(2));
        var reopened = service.ChangeStatus(task.Id, TaskStatus.Ready);

        Assert.Equal(TaskStatus.Ready, reopened.Status);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public void ChangeStatus_DoneToWaiting_Throws()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var task = service.Create("Terminal");
        service.CompleteTask(task.Id);

        Assert.Throws<InvalidOperationException>(() =>
            service.ChangeStatus(task.Id, TaskStatus.Waiting));
    }

    [Fact]
    public void ChangeStatus_SameStatus_IsNoOp()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);
        var task = service.Create("Same");
        service.ChangeStatus(task.Id, TaskStatus.Ready);
        setNow(start.AddMinutes(10));

        var result = service.ChangeStatus(task.Id, TaskStatus.Ready);

        Assert.Equal(TaskStatus.Ready, result.Status);
        Assert.Equal(start, result.UpdatedAt);
    }

    [Fact]
    public void Create_WithPlannedOrigin_PersistsOrigin()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        var task = service.Create("Planned work", origin: TaskOrigin.Planned);

        Assert.Equal(TaskStatus.Inbox, task.Status);
        Assert.Equal(TaskOrigin.Planned, task.Origin);
    }

    [Fact]
    public void CaptureToInbox_CreatesInboxTaskWithUnplannedOrigin()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, store, _) = CreateHarness(start);

        var task = service.CaptureToInbox("Quick idea");

        Assert.Equal("Quick idea", task.Title);
        Assert.Equal(TaskStatus.Inbox, task.Status);
        Assert.Equal(TaskOrigin.Unplanned, task.Origin);
        Assert.Equal(start, task.CreatedAt);
        Assert.Null(task.ProjectId);

        var loaded = store.Get(task.Id);
        Assert.NotNull(loaded);
        Assert.Equal(TaskStatus.Inbox, loaded!.Status);
    }

    [Fact]
    public void CaptureToInbox_WithBlankTitle_Throws()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => service.CaptureToInbox("   "));
        Assert.Throws<ArgumentException>(() => service.CaptureToInbox(""));
    }

    [Fact]
    public void CaptureToInbox_DoesNotDisturbRunningTask()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var running = service.Create("Running task");
        service.StartTask(running.Id);

        var captured = service.CaptureToInbox("New capture");

        var stillRunning = service.GetRunningTask();
        Assert.NotNull(stillRunning);
        Assert.Equal(running.Id, stillRunning!.Id);
        Assert.Equal(TaskStatus.Running, service.Get(running.Id)!.Status);
        Assert.Equal(TaskStatus.Inbox, captured.Status);
        Assert.Equal(TaskOrigin.Unplanned, captured.Origin);
    }

    [Fact]
    public void CaptureToInbox_WithProject_AssignsProject()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => store.List());
        var service = new TaskService(store, projectStore, () => start);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Jetset",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(project);

        var task = service.CaptureToInbox("Scoped capture", project.Id);

        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal(TaskStatus.Inbox, task.Status);
        Assert.Equal(TaskOrigin.Unplanned, task.Origin);
    }

    [Fact]
    public void CompleteTask_SetsDoneAndCompletedAt()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);
        var task = service.Create("Finish me");
        setNow(start.AddMinutes(3));

        var completed = service.CompleteTask(task.Id);

        Assert.Equal(TaskStatus.Done, completed.Status);
        Assert.Equal(start.AddMinutes(3), completed.CompletedAt);
    }

    [Fact]
    public void StartTask_SetsRunningAndRecordsLastWorkedAt()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);
        var task = service.Create("Run me");
        setNow(start.AddMinutes(2));

        var running = service.StartTask(task.Id);

        Assert.Equal(TaskStatus.Running, running.Status);
        Assert.Equal(start.AddMinutes(2), running.LastWorkedAt);
        Assert.Equal(task.Id, service.GetRunningTask()!.Id);
    }

    [Fact]
    public void StartTask_WhenAnotherTaskIsRunning_LeavesPreviousAsReadyByDefault()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var first = service.Create("First");
        var second = service.Create("Second");

        service.StartTask(first.Id);
        service.StartTask(second.Id);

        Assert.Equal(TaskStatus.Ready, service.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, service.Get(second.Id)!.Status);
        Assert.Equal(second.Id, service.GetRunningTask()!.Id);
    }

    [Fact]
    public void StartTask_WhenAnotherTaskIsRunning_CanLeavePreviousAsWaiting()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var first = service.Create("First");
        var second = service.Create("Second");

        service.StartTask(first.Id);
        service.StartTask(second.Id, leavingStatus: TaskStatus.Waiting);

        Assert.Equal(TaskStatus.Waiting, service.Get(first.Id)!.Status);
        Assert.Equal(TaskStatus.Running, service.Get(second.Id)!.Status);
    }

    [Fact]
    public void StartTask_FromWaiting_PreservesWaitingWhenSwitchedWithDefaultLeavingStatus()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var waiting = service.Create("Waiting task");
        var other = service.Create("Other task");
        var interrupt = service.Create("Interrupt");

        service.ChangeStatus(waiting.Id, TaskStatus.Waiting);
        service.StartTask(waiting.Id);
        service.StartTask(other.Id);
        service.StartTask(interrupt.Id);

        Assert.Equal(TaskStatus.Waiting, service.Get(waiting.Id)!.Status);
        Assert.Equal(TaskStatus.Ready, service.Get(other.Id)!.Status);
        Assert.Equal(TaskStatus.Running, service.Get(interrupt.Id)!.Status);
    }

    [Fact]
    public void StartTask_OnlyOneRunningTask_Enforced()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var a = service.Create("A");
        var b = service.Create("B");
        var c = service.Create("C");

        service.StartTask(a.Id);
        service.StartTask(b.Id);
        service.StartTask(c.Id);

        var running = service.List().Where(t => t.Status == TaskStatus.Running).ToList();
        Assert.Single(running);
        Assert.Equal(c.Id, running[0].Id);
    }

    [Fact]
    public void StartTask_FromDone_Throws()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var task = service.Create("Done");
        service.CompleteTask(task.Id);

        Assert.Throws<InvalidOperationException>(() => service.StartTask(task.Id));
    }

    [Fact]
    public void StopTask_FromRunning_ReturnsToReady()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var task = service.Create("Running");
        service.StartTask(task.Id);

        var stopped = service.StopTask(task.Id);

        Assert.Equal(TaskStatus.Ready, stopped.Status);
        Assert.Null(service.GetRunningTask());
    }

    [Fact]
    public void ListActiveWork_IncludesReadyWaitingAndInboxOnly()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var ready = service.Create("Ready");
        service.ChangeStatus(ready.Id, TaskStatus.Ready);
        var waiting = service.Create("Waiting");
        var inbox = service.Create("Inbox");
        var running = service.Create("Running");
        var done = service.Create("Done");
        var cancelled = service.Create("Cancelled");

        service.ChangeStatus(waiting.Id, TaskStatus.Waiting);
        service.StartTask(running.Id);
        service.CompleteTask(done.Id);
        service.ChangeStatus(cancelled.Id, TaskStatus.Cancelled);

        var activeWork = service.ListActiveWork();

        Assert.Equal(3, activeWork.Count);
        Assert.Contains(activeWork, t => t.Id == ready.Id);
        Assert.Contains(activeWork, t => t.Id == waiting.Id);
        Assert.Contains(activeWork, t => t.Id == inbox.Id);
        Assert.DoesNotContain(activeWork, t => t.Id == running.Id);
        Assert.DoesNotContain(activeWork, t => t.Id == done.Id);
        Assert.DoesNotContain(activeWork, t => t.Id == cancelled.Id);

        Assert.True(service.IsEligibleForActiveWork(service.Get(ready.Id)!));
        Assert.True(service.IsEligibleForActiveWork(service.Get(waiting.Id)!));
        Assert.True(service.IsEligibleForActiveWork(service.Get(inbox.Id)!));
        Assert.False(service.IsEligibleForActiveWork(service.Get(done.Id)!));
    }

    [Fact]
    public void Delete_RemovesTask()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        var task = service.Create("To delete");
        service.Delete(task.Id);

        Assert.Null(service.Get(task.Id));
        Assert.Empty(service.List());
    }

    [Fact]
    public void Search_MatchesTitleSubstring()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        service.Create("Review PR");
        service.Create("Reply Email");
        service.Create("Investigate bug");

        var results = service.Search("pr");

        Assert.Single(results);
        Assert.Equal("Review PR", results[0].Title);
    }

    [Fact]
    public void Search_WithEmptyQuery_ReturnsEmpty()
    {
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        service.Create("Something");

        Assert.Empty(service.Search(""));
        Assert.Empty(service.Search("   "));
    }

    [Fact]
    public void Search_MatchesProjectContextText()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, _) = CreateHarnessWithProjects(start);
        var project = InsertProject(projectStore, "Jetset", start);
        projectStore.Update(new Project
        {
            Id = project.Id,
            Name = project.Name,
            ContextText = "Resume with API design notes",
            ContextUpdatedAt = start,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        });

        var task = service.Create("Implement endpoint", project.Id);
        service.Create("Unrelated inbox item");

        var results = service.Search("API design");

        Assert.Single(results);
        Assert.Equal(task.Id, results[0].Id);
    }

    [Fact]
    public void Search_MatchesProjectContextText_ForAllTasksOnProject()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, _) = CreateHarnessWithProjects(start);
        var project = InsertProject(projectStore, "Backend", start);
        projectStore.Update(new Project
        {
            Id = project.Id,
            Name = project.Name,
            ContextText = "OAuth migration checklist",
            ContextUpdatedAt = start,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        });

        var first = service.Create("Wire auth handler", project.Id);
        var second = service.Create("Update token refresh", project.Id);
        service.Create("Buy groceries");

        var results = service.Search("OAuth");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, t => t.Id == first.Id);
        Assert.Contains(results, t => t.Id == second.Id);
    }

    [Fact]
    public void TaskStore_Search_MatchesProjectContextText()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        using (var connection = factory.Create())
        {
            using var projectCommand = connection.CreateCommand();
            projectCommand.CommandText =
                """
                INSERT INTO Project (Id, Name, Deadline, ContextText, ContextUpdatedAt, CreatedAt, UpdatedAt)
                VALUES (@id, @name, NULL, @contextText, @contextUpdatedAt, @createdAt, @updatedAt);
                """;
            projectCommand.Parameters.AddWithValue("@id", projectId.ToString());
            projectCommand.Parameters.AddWithValue("@name", "Jetset");
            projectCommand.Parameters.AddWithValue("@contextText", "Ship dashboard widgets");
            projectCommand.Parameters.AddWithValue("@contextUpdatedAt", now.ToString("O"));
            projectCommand.Parameters.AddWithValue("@createdAt", now.ToString("O"));
            projectCommand.Parameters.AddWithValue("@updatedAt", now.ToString("O"));
            projectCommand.ExecuteNonQuery();

            using var taskCommand = connection.CreateCommand();
            taskCommand.CommandText =
                """
                INSERT INTO "Task" (
                    Id, Title, Status, Origin, Notes, ProjectId,
                    CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt)
                VALUES (
                    @id, @title, @status, @origin, NULL, @projectId,
                    @createdAt, NULL, @updatedAt, NULL);
                """;
            taskCommand.Parameters.AddWithValue("@id", taskId.ToString());
            taskCommand.Parameters.AddWithValue("@title", "Polish layout");
            taskCommand.Parameters.AddWithValue("@status", (int)TaskStatus.Ready);
            taskCommand.Parameters.AddWithValue("@origin", (int)TaskOrigin.Planned);
            taskCommand.Parameters.AddWithValue("@projectId", projectId.ToString());
            taskCommand.Parameters.AddWithValue("@createdAt", now.ToString("O"));
            taskCommand.Parameters.AddWithValue("@updatedAt", now.ToString("O"));
            taskCommand.ExecuteNonQuery();
        }

        var store = new TaskStore(factory);
        var results = store.Search("dashboard");

        Assert.Single(results);
        Assert.Equal(taskId, results[0].Id);
        Assert.Equal("Polish layout", results[0].Title);
    }

    [Fact]
    public void AssignToProject_LinksTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => store.List());
        var service = new TaskService(store, projectStore, () => start);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Jetset",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(project);

        var task = service.Create("Review PR");
        var setNow = start;
        var updated = service.AssignToProject(task.Id, project.Id);
        setNow = start.AddMinutes(2);

        Assert.Equal(project.Id, updated.ProjectId);
        Assert.Equal(project.Id, service.Get(task.Id)!.ProjectId);
    }

    [Fact]
    public void TaskStore_PersistsAcrossStoreInstances()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        var id = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var firstStore = new TaskStore(factory);
        firstStore.Insert(new WorkTask
        {
            Id = id,
            Title = "Persisted task",
            Status = TaskStatus.Ready,
            Origin = TaskOrigin.Planned,
            CreatedAt = now,
            UpdatedAt = now
        });

        var secondStore = new TaskStore(factory);
        var loaded = secondStore.Get(id);

        Assert.NotNull(loaded);
        Assert.Equal("Persisted task", loaded!.Title);
        Assert.Equal(TaskStatus.Ready, loaded.Status);
        Assert.Equal(TaskOrigin.Planned, loaded.Origin);
    }

    [Fact]
    public void Migration008_RemapsLegacyStatuses()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);

        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable()
        ]).RunPending();

        var activeId = Guid.NewGuid().ToString();
        var blockedId = Guid.NewGuid().ToString();
        var doneId = Guid.NewGuid().ToString();
        var cancelledId = Guid.NewGuid().ToString();
        var now = "2026-08-22T10:00:00.0000000+00:00";

        using (var connection = factory.Create())
        {
            InsertLegacyTask(connection, activeId, "Active task", 0, now);
            InsertLegacyTask(connection, blockedId, "Blocked task", 1, now);
            InsertLegacyTask(connection, doneId, "Done task", 2, now);
            InsertLegacyTask(connection, cancelledId, "Cancelled task", 3, now);
        }

        new MigrationRunner(factory, [new Migration008_TaskLifecycleRealignment()]).RunPending();

        var store = new TaskStore(factory);
        Assert.Equal(TaskStatus.Ready, store.Get(Guid.Parse(activeId))!.Status);
        Assert.Equal(TaskStatus.Waiting, store.Get(Guid.Parse(blockedId))!.Status);
        Assert.Equal(TaskStatus.Done, store.Get(Guid.Parse(doneId))!.Status);
        Assert.NotNull(store.Get(Guid.Parse(doneId))!.CompletedAt);
        Assert.Equal(TaskStatus.Cancelled, store.Get(Guid.Parse(cancelledId))!.Status);
        Assert.Equal(TaskOrigin.Unplanned, store.Get(Guid.Parse(activeId))!.Origin);
    }

    private static void InsertLegacyTask(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string id,
        string title,
        int legacyStatus,
        string timestamp)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "Task" (
                Id, Title, Status, Notes, CurrentStatus, LastProgress, NextAction, Blocker,
                ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt)
            VALUES (
                @id, @title, @status, NULL, NULL, NULL, NULL, NULL,
                NULL, NULL, @createdAt, @updatedAt, NULL);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@status", legacyStatus);
        command.Parameters.AddWithValue("@createdAt", timestamp);
        command.Parameters.AddWithValue("@updatedAt", timestamp);
        command.ExecuteNonQuery();
    }
}
