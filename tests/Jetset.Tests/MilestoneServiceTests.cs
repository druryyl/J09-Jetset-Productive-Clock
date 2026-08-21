using System.IO;
using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

public class MilestoneServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static MilestoneServiceTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public MilestoneServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetMilestoneTests", Guid.NewGuid().ToString("N"));
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

    private static (
        MilestoneService Milestones,
        ProjectService Projects,
        TaskService Tasks,
        InMemoryMilestoneStore MilestoneStore,
        InMemoryTaskStore TaskStore,
        Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var taskStore = new InMemoryTaskStore();
        var projectStore = new InMemoryProjectStore(() => taskStore.List());
        var milestoneStore = new InMemoryMilestoneStore();
        var projects = new ProjectService(projectStore, taskStore, milestoneStore, () => now);
        var tasks = new TaskService(taskStore, projectStore, milestoneStore, () => now);
        var milestones = new MilestoneService(milestoneStore, projectStore, taskStore, () => now);
        return (milestones, projects, tasks, milestoneStore, taskStore, value => now = value);
    }

    [Fact]
    public void Create_WithName_PersistsMilestoneWithAutoSortOrder()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (milestones, projects, _, store, _, _) = CreateHarness(start);
        var project = projects.Create("Jetset");

        var first = milestones.Create(project.Id, "Domain Design");
        var second = milestones.Create(project.Id, "Architecture");

        Assert.Equal("Domain Design", first.Name);
        Assert.Equal(0, first.SortOrder);
        Assert.Equal(start, first.CreatedAt);
        Assert.Equal(1, second.SortOrder);

        var loaded = store.Get(first.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Domain Design", loaded!.Name);
        Assert.Equal(project.Id, loaded.ProjectId);
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        var (milestones, projects, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("Jetset");

        Assert.Throws<ArgumentException>(() => milestones.Create(project.Id, "   "));
        Assert.Throws<ArgumentException>(() => milestones.Create(project.Id, ""));
    }

    [Fact]
    public void ListByProject_ReturnsMilestonesInSortOrder()
    {
        var (milestones, projects, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("Jetset");
        var a = milestones.Create(project.Id, "A");
        var b = milestones.Create(project.Id, "B");
        var c = milestones.Create(project.Id, "C");

        milestones.Reorder(project.Id, [c.Id, a.Id, b.Id]);

        var list = milestones.ListByProject(project.Id);
        Assert.Equal(3, list.Count);
        Assert.Equal(c.Id, list[0].Id);
        Assert.Equal(a.Id, list[1].Id);
        Assert.Equal(b.Id, list[2].Id);
        Assert.Equal(0, list[0].SortOrder);
        Assert.Equal(1, list[1].SortOrder);
        Assert.Equal(2, list[2].SortOrder);
    }

    [Fact]
    public void Reorder_UpdatesSortOrderValues()
    {
        var (milestones, projects, _, store, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("Jetset");
        var a = milestones.Create(project.Id, "A");
        var b = milestones.Create(project.Id, "B");

        milestones.Reorder(project.Id, [b.Id, a.Id]);

        Assert.Equal(0, store.Get(b.Id)!.SortOrder);
        Assert.Equal(1, store.Get(a.Id)!.SortOrder);
    }

    [Fact]
    public void GetProgress_ReturnsDoneOverTotal()
    {
        var (milestones, projects, tasks, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("Jetset");
        var milestone = milestones.Create(project.Id, "Domain");

        for (var i = 0; i < 5; i++)
        {
            var task = tasks.Create($"Task {i}", project.Id);
            tasks.AssignToMilestone(task.Id, milestone.Id);
        }

        var all = tasks.ListByProject(project.Id);
        all[0].Status = TaskStatus.Done;
        tasks.Update(all[0]);
        all[1].Status = TaskStatus.Done;
        tasks.Update(all[1]);

        var progress = milestones.GetProgress(milestone.Id);
        Assert.Equal(2, progress.DoneCount);
        Assert.Equal(5, progress.TotalCount);
        Assert.Equal(0.4, progress.Fraction, 5);
    }

    [Fact]
    public void GetProgress_WithZeroTasks_ReturnsZero()
    {
        var (milestones, projects, _, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("Jetset");
        var milestone = milestones.Create(project.Id, "Empty");

        var progress = milestones.GetProgress(milestone.Id);
        Assert.Equal(0, progress.DoneCount);
        Assert.Equal(0, progress.TotalCount);
        Assert.Equal(0, progress.Fraction);
    }

    [Fact]
    public void Delete_UnassignsTasksButKeepsProject()
    {
        var (milestones, projects, tasks, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("Jetset");
        var milestone = milestones.Create(project.Id, "Domain");
        var task = tasks.Create("Linked", project.Id);
        tasks.AssignToMilestone(task.Id, milestone.Id);

        milestones.Delete(milestone.Id);

        Assert.Null(milestones.Get(milestone.Id));
        var reloaded = tasks.Get(task.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(project.Id, reloaded!.ProjectId);
        Assert.Null(reloaded.MilestoneId);
    }

    [Fact]
    public void AssignToMilestone_SetsMilestoneIdWhenProjectMatches()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (milestones, projects, tasks, _, _, setNow) = CreateHarness(start);
        var project = projects.Create("Jetset");
        var milestone = milestones.Create(project.Id, "Domain");
        var task = tasks.Create("Work", project.Id);
        setNow(start.AddMinutes(3));

        var updated = tasks.AssignToMilestone(task.Id, milestone.Id);

        Assert.Equal(milestone.Id, updated.MilestoneId);
        Assert.Equal(start.AddMinutes(3), updated.UpdatedAt);
    }

    [Fact]
    public void AssignToMilestone_WrongProject_Throws()
    {
        var (milestones, projects, tasks, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var projectA = projects.Create("A");
        var projectB = projects.Create("B");
        var milestoneB = milestones.Create(projectB.Id, "B milestone");
        var taskA = tasks.Create("In A", projectA.Id);

        Assert.Throws<InvalidOperationException>(() =>
            tasks.AssignToMilestone(taskA.Id, milestoneB.Id));
    }

    [Fact]
    public void AssignToMilestone_WithoutProject_Throws()
    {
        var (milestones, projects, tasks, _, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("Jetset");
        var milestone = milestones.Create(project.Id, "Domain");
        var task = tasks.Create("Unassigned");

        Assert.Throws<InvalidOperationException>(() =>
            tasks.AssignToMilestone(task.Id, milestone.Id));
    }

    [Fact]
    public void DeleteProject_RemovesMilestones()
    {
        var (milestones, projects, tasks, milestoneStore, _, _) = CreateHarness(DateTimeOffset.UtcNow);
        var project = projects.Create("To delete");
        var milestone = milestones.Create(project.Id, "Domain");
        var task = tasks.Create("Linked", project.Id);
        tasks.AssignToMilestone(task.Id, milestone.Id);

        projects.Delete(project.Id);

        Assert.Null(projects.Get(project.Id));
        Assert.Null(milestoneStore.Get(milestone.Id));
        Assert.Empty(milestoneStore.ListByProject(project.Id));
        Assert.Null(tasks.Get(task.Id)!.MilestoneId);
        Assert.Null(tasks.Get(task.Id)!.ProjectId);
    }

    [Fact]
    public void MilestoneStore_PersistsAcrossStoreInstances()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

        var projectStore = new ProjectStore(factory);
        projectStore.Insert(new Project
        {
            Id = projectId,
            Name = "Parent",
            CreatedAt = now,
            UpdatedAt = now
        });

        var firstStore = new MilestoneStore(factory);
        firstStore.Insert(new Milestone
        {
            Id = milestoneId,
            ProjectId = projectId,
            Name = "Persisted milestone",
            SortOrder = 0,
            CreatedAt = now
        });

        var secondStore = new MilestoneStore(factory);
        var loaded = secondStore.Get(milestoneId);

        Assert.NotNull(loaded);
        Assert.Equal("Persisted milestone", loaded!.Name);
        Assert.Equal(projectId, loaded.ProjectId);
        Assert.Equal(0, loaded.SortOrder);

        var listed = secondStore.ListByProject(projectId);
        Assert.Single(listed);
    }
}
