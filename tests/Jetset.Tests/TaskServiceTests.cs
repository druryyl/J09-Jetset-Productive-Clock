using System.IO;
using Jetset.App.Models;
using Jetset.App.Persistence;
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

    private static (TaskService Service, InMemoryTaskStore Store, InMemoryProjectStore ProjectStore, InMemoryMilestoneStore MilestoneStore, Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var store = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => store.List());
        var milestoneStore = new InMemoryMilestoneStore();
        var service = new TaskService(store, projectStore, milestoneStore, () => now);
        return (service, store, projectStore, milestoneStore, value => now = value);
    }

    [Fact]
    public void Create_WithTitle_PersistsActiveTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, store, _, _, _) = CreateHarness(start);

        var task = service.Create("Review PR");

        Assert.Equal("Review PR", task.Title);
        Assert.Equal(TaskStatus.Active, task.Status);
        Assert.Equal(start, task.CreatedAt);
        Assert.Equal(start, task.UpdatedAt);
        Assert.Null(task.ProjectId);

        var loaded = store.Get(task.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Review PR", loaded!.Title);
    }

    [Fact]
    public void Create_WithBlankTitle_Throws()
    {
        var (service, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => service.Create("   "));
        Assert.Throws<ArgumentException>(() => service.Create(""));
    }

    [Fact]
    public void Update_ChangesTitleAndNotes()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, _, _, setNow) = CreateHarness(start);

        var task = service.Create("Original");
        setNow(start.AddMinutes(5));

        task.Title = "Updated title";
        task.Notes = "Some notes";
        task.Status = TaskStatus.Blocked;

        var updated = service.Update(task);

        Assert.Equal("Updated title", updated.Title);
        Assert.Equal("Some notes", updated.Notes);
        Assert.Equal(TaskStatus.Blocked, updated.Status);
        Assert.Equal(start, updated.CreatedAt);
        Assert.Equal(start.AddMinutes(5), updated.UpdatedAt);
    }

    [Fact]
    public void Delete_RemovesTask()
    {
        var (service, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        var task = service.Create("To delete");
        service.Delete(task.Id);

        Assert.Null(service.Get(task.Id));
        Assert.Empty(service.List());
    }

    [Fact]
    public void Search_MatchesTitleSubstring()
    {
        var (service, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

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
        var (service, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        service.Create("Something");

        Assert.Empty(service.Search(""));
        Assert.Empty(service.Search("   "));
    }

    [Fact]
    public void AssignToProject_LinksTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, _, setNow) = CreateHarness(start);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Jetset",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(project);

        var task = service.Create("Review PR");
        setNow(start.AddMinutes(2));

        var updated = service.AssignToProject(task.Id, project.Id);

        Assert.Equal(project.Id, updated.ProjectId);
        Assert.Equal(start.AddMinutes(2), updated.UpdatedAt);
        Assert.Equal(project.Id, service.Get(task.Id)!.ProjectId);
    }

    [Fact]
    public void AssignToProject_WithInvalidProject_Throws()
    {
        var (service, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var task = service.Create("Orphan");

        Assert.Throws<InvalidOperationException>(() =>
            service.AssignToProject(task.Id, Guid.NewGuid()));
    }

    [Fact]
    public void UnassignFromProject_ClearsProjectId()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, milestoneStore, _) = CreateHarness(start);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Jetset",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(project);
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Domain",
            SortOrder = 0,
            CreatedAt = start
        };
        milestoneStore.Insert(milestone);

        var task = service.Create("Review PR", project.Id);
        service.AssignToMilestone(task.Id, milestone.Id);

        var updated = service.AssignToProject(task.Id, null);

        Assert.Null(updated.ProjectId);
        Assert.Null(updated.MilestoneId);
    }

    [Fact]
    public void ListByProject_FiltersCorrectly()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, _, _) = CreateHarness(start);
        var projectA = new Project
        {
            Id = Guid.NewGuid(),
            Name = "A",
            CreatedAt = start,
            UpdatedAt = start
        };
        var projectB = new Project
        {
            Id = Guid.NewGuid(),
            Name = "B",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(projectA);
        projectStore.Insert(projectB);

        service.Create("In A", projectA.Id);
        service.Create("Also in A", projectA.Id);
        service.Create("In B", projectB.Id);
        service.Create("Unassigned");

        Assert.Equal(2, service.ListByProject(projectA.Id).Count);
        Assert.Single(service.ListByProject(projectB.Id));
        Assert.Single(service.ListByProject(null));
        Assert.Equal(4, service.List().Count);
    }

    [Fact]
    public void AssignToMilestone_LinksTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, milestoneStore, setNow) = CreateHarness(start);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Jetset",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(project);
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Domain",
            SortOrder = 0,
            CreatedAt = start
        };
        milestoneStore.Insert(milestone);

        var task = service.Create("Review PR", project.Id);
        setNow(start.AddMinutes(2));

        var updated = service.AssignToMilestone(task.Id, milestone.Id);

        Assert.Equal(milestone.Id, updated.MilestoneId);
        Assert.Equal(start.AddMinutes(2), updated.UpdatedAt);
    }

    [Fact]
    public void AssignToMilestone_WrongProject_Throws()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, milestoneStore, _) = CreateHarness(start);
        var projectA = new Project
        {
            Id = Guid.NewGuid(),
            Name = "A",
            CreatedAt = start,
            UpdatedAt = start
        };
        var projectB = new Project
        {
            Id = Guid.NewGuid(),
            Name = "B",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(projectA);
        projectStore.Insert(projectB);
        milestoneStore.Insert(new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = projectB.Id,
            Name = "B milestone",
            SortOrder = 0,
            CreatedAt = start
        });
        var milestoneB = milestoneStore.ListByProject(projectB.Id)[0];
        var task = service.Create("In A", projectA.Id);

        Assert.Throws<InvalidOperationException>(() =>
            service.AssignToMilestone(task.Id, milestoneB.Id));
    }

    [Fact]
    public void AssignToMilestone_ClearsAssignment()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, projectStore, milestoneStore, _) = CreateHarness(start);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Jetset",
            CreatedAt = start,
            UpdatedAt = start
        };
        projectStore.Insert(project);
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Domain",
            SortOrder = 0,
            CreatedAt = start
        };
        milestoneStore.Insert(milestone);

        var task = service.Create("Review PR", project.Id);
        service.AssignToMilestone(task.Id, milestone.Id);

        var updated = service.AssignToMilestone(task.Id, null);

        Assert.Null(updated.MilestoneId);
        Assert.Equal(project.Id, updated.ProjectId);
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
            Status = TaskStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        var secondStore = new TaskStore(factory);
        var loaded = secondStore.Get(id);

        Assert.NotNull(loaded);
        Assert.Equal("Persisted task", loaded!.Title);
        Assert.Equal(TaskStatus.Active, loaded.Status);
    }
}
