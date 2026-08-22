using System.IO;
using Jetset.App.Persistence;

namespace Jetset.Tests;

public class TreeStateStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static TreeStateStoreTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public TreeStateStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetTreeStateTests", Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void InMemory_GetExpandedProjectIds_ReturnsEmptyByDefault()
    {
        var store = new InMemoryTreeStateStore();

        Assert.Empty(store.GetExpandedProjectIds());
        Assert.False(store.IsExpanded(Guid.NewGuid()));
    }

    [Fact]
    public void InMemory_SetExpanded_TracksExpandAndCollapse()
    {
        var store = new InMemoryTreeStateStore();
        var projectId = Guid.NewGuid();

        store.SetExpanded(projectId, expanded: true);

        Assert.True(store.IsExpanded(projectId));
        Assert.Single(store.GetExpandedProjectIds());
        Assert.Contains(projectId, store.GetExpandedProjectIds());

        store.SetExpanded(projectId, expanded: false);

        Assert.False(store.IsExpanded(projectId));
        Assert.Empty(store.GetExpandedProjectIds());
    }

    [Fact]
    public void TreeStateStore_PersistsExpandedProjectIdsAcrossInstances()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var firstStore = new TreeStateStore(factory);
        firstStore.SetExpanded(projectA, expanded: true);
        firstStore.SetExpanded(projectB, expanded: true);

        var secondStore = new TreeStateStore(factory);
        var expanded = secondStore.GetExpandedProjectIds();

        Assert.Equal(2, expanded.Count);
        Assert.Contains(projectA, expanded);
        Assert.Contains(projectB, expanded);
        Assert.True(secondStore.IsExpanded(projectA));
        Assert.True(secondStore.IsExpanded(projectB));
    }

    [Fact]
    public void TreeStateStore_CollapseRemovesProjectFromPersistedState()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        var projectId = Guid.NewGuid();

        var firstStore = new TreeStateStore(factory);
        firstStore.SetExpanded(projectId, expanded: true);
        firstStore.SetExpanded(projectId, expanded: false);

        var secondStore = new TreeStateStore(factory);

        Assert.Empty(secondStore.GetExpandedProjectIds());
        Assert.False(secondStore.IsExpanded(projectId));
    }

    [Fact]
    public void TreeStateStore_StoresStateInAppSettingNotDomainTables()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        var projectId = Guid.NewGuid();
        var store = new TreeStateStore(factory);
        store.SetExpanded(projectId, expanded: true);

        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSetting WHERE Key = 'WorkTreeExpandedProjects';";
        var value = command.ExecuteScalar() as string;

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Contains(projectId.ToString(), value, StringComparison.OrdinalIgnoreCase);

        using var projectColumns = connection.CreateCommand();
        projectColumns.CommandText = "PRAGMA table_info(Project);";
        using var reader = projectColumns.ExecuteReader();
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            Assert.DoesNotContain("expand", columnName, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TreeStateStore_ReturnsEmptyWhenSettingMissingOrInvalid()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        var factory = new SqliteConnectionFactory(path);
        new SchemaInitializer(factory).Initialize();

        Assert.Empty(new TreeStateStore(factory).GetExpandedProjectIds());

        using (var connection = factory.Create())
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                INSERT INTO AppSetting (Key, Value) VALUES ('WorkTreeExpandedProjects', 'not-json');
                """;
            command.ExecuteNonQuery();
        }

        Assert.Empty(new TreeStateStore(factory).GetExpandedProjectIds());
    }
}
