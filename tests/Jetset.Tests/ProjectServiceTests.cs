using System.IO;
using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;

namespace Jetset.Tests;

public class ProjectServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static ProjectServiceTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public ProjectServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetProjectTests", Guid.NewGuid().ToString("N"));
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

    private static (ProjectService Projects, TaskService Tasks, InMemoryProjectStore ProjectStore, InMemoryTaskStore TaskStore, InMemoryMilestoneStore MilestoneStore, Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var taskStore = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => taskStore.List());
        var milestoneStore = new InMemoryMilestoneStore();
        var projects = new ProjectService(projectStore, taskStore, milestoneStore, () => now);
        var tasks = new TaskService(taskStore, projectStore, milestoneStore, () => now);
        return (projects, tasks, projectStore, taskStore, milestoneStore, value => now = value);
    }

    [Fact]
    public void Create_WithName_PersistsProject()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (projects, _, store, _, _, _) = CreateHarness(start);

        var project = projects.Create("Jetset");

        Assert.Equal("Jetset", project.Name);
        Assert.Null(project.Deadline);
        Assert.Equal(start, project.CreatedAt);
        Assert.Equal(start, project.UpdatedAt);

        var loaded = store.Get(project.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Jetset", loaded!.Name);
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        var (projects, _, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => projects.Create("   "));
        Assert.Throws<ArgumentException>(() => projects.Create(""));
    }

    [Fact]
    public void Create_WithDeadline_StoresDate()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (projects, _, store, _, _, _) = CreateHarness(start);
        var deadline = new DateOnly(2026, 9, 30);

        var project = projects.Create("School SIS", deadline);

        Assert.Equal(deadline, project.Deadline);
        Assert.Equal(deadline, store.Get(project.Id)!.Deadline);
    }

    [Fact]
    public void Update_ChangesNameAndDeadline()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (projects, _, _, _, _, setNow) = CreateHarness(start);

        var project = projects.Create("Original");
        setNow(start.AddMinutes(5));

        project.Name = "Renamed";
        project.Deadline = new DateOnly(2026, 12, 1);
        var updated = projects.Update(project);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(new DateOnly(2026, 12, 1), updated.Deadline);
        Assert.Equal(start, updated.CreatedAt);
        Assert.Equal(start.AddMinutes(5), updated.UpdatedAt);
    }

    [Fact]
    public void Delete_UnassignsTasksThenRemovesProject()
    {
        var (projects, tasks, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        var project = projects.Create("To delete");
        var task = tasks.Create("Linked task", project.Id);
        Assert.Equal(project.Id, task.ProjectId);

        projects.Delete(project.Id);

        Assert.Null(projects.Get(project.Id));
        var reloaded = tasks.Get(task.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.ProjectId);
        Assert.Null(reloaded.MilestoneId);
    }

    [Fact]
    public void List_ReturnsTaskCounts()
    {
        var (projects, tasks, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        var empty = projects.Create("Empty");
        var one = projects.Create("One task");
        var many = projects.Create("Many tasks");

        tasks.Create("A", one.Id);
        tasks.Create("B", many.Id);
        tasks.Create("C", many.Id);
        tasks.Create("Unassigned");

        var list = projects.List();

        Assert.Equal(0, list.Single(s => s.Project.Id == empty.Id).TaskCount);
        Assert.Equal(1, list.Single(s => s.Project.Id == one.Id).TaskCount);
        Assert.Equal(2, list.Single(s => s.Project.Id == many.Id).TaskCount);
    }

    [Fact]
    public void ProjectStore_PersistsAcrossStoreInstances()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        var id = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var deadline = new DateOnly(2026, 10, 15);
        var firstStore = new ProjectStore(factory);
        firstStore.Insert(new Project
        {
            Id = id,
            Name = "Persisted project",
            Deadline = deadline,
            CreatedAt = now,
            UpdatedAt = now
        });

        var secondStore = new ProjectStore(factory);
        var loaded = secondStore.Get(id);

        Assert.NotNull(loaded);
        Assert.Equal("Persisted project", loaded!.Name);
        Assert.Equal(deadline, loaded.Deadline);

        var summaries = secondStore.ListWithTaskCounts();
        Assert.Single(summaries);
        Assert.Equal(0, summaries[0].TaskCount);
    }
}
