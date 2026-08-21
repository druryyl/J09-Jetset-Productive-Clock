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

    private static (TaskService Service, InMemoryTaskStore Store, Action<DateTimeOffset> SetNow)
        CreateHarness(DateTimeOffset start)
    {
        var now = start;
        var store = new InMemoryTaskStore();
        var service = new TaskService(store, () => now);
        return (service, store, value => now = value);
    }

    [Fact]
    public void Create_WithTitle_PersistsActiveTask()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, store, _) = CreateHarness(start);

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
        var (service, _, _) = CreateHarness(DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => service.Create("   "));
        Assert.Throws<ArgumentException>(() => service.Create(""));
    }

    [Fact]
    public void Update_ChangesTitleAndNotes()
    {
        var start = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var (service, _, setNow) = CreateHarness(start);

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
