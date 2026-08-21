using System.IO;
using Jetset.App.Persistence;
using Jetset.App.Persistence.Migrations;

namespace Jetset.Tests;

public class MigrationRunnerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static MigrationRunnerTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public MigrationRunnerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetMigrationTests", Guid.NewGuid().ToString("N"));
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
    public void GivenFreshDatabase_WhenRunnerRuns_ThenVersionIs1AndTablesExist()
    {
        var factory = CreateFactory();

        RunMigrations(factory);

        Assert.Equal(1, GetSchemaVersion(factory));
        Assert.True(TableExists(factory, "WorkSession"));
        Assert.True(TableExists(factory, "WorkInterval"));
        Assert.True(TableExists(factory, "AppSetting"));
        Assert.True(TableExists(factory, "SchemaVersion"));
    }

    [Fact]
    public void GivenFreshDatabase_WhenRunnerRunsTwice_ThenIsIdempotent()
    {
        var factory = CreateFactory();

        RunMigrations(factory);
        RunMigrations(factory);

        Assert.Equal(1, GetSchemaVersion(factory));
        Assert.Equal(1, CountSchemaVersionRows(factory));
    }

    [Fact]
    public void GivenLegacyV1Database_WhenRunnerRuns_ThenDataPreservedAndVersionRecorded()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, sessionId: "legacy-session-1", taskName: "Legacy Task");

        RunMigrations(factory);

        Assert.Equal(1, GetSchemaVersion(factory));
        Assert.Equal("Legacy Task", GetTaskName(factory, "legacy-session-1"));
    }

    [Fact]
    public void GivenLegacyV1Database_WhenRunnerRunsTwice_ThenIsIdempotent()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, sessionId: "legacy-session-2", taskName: "Still Here");

        RunMigrations(factory);
        RunMigrations(factory);

        Assert.Equal(1, GetSchemaVersion(factory));
        Assert.Equal(1, CountSchemaVersionRows(factory));
        Assert.Equal("Still Here", GetTaskName(factory, "legacy-session-2"));
    }

    private SqliteConnectionFactory CreateFactory()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new SqliteConnectionFactory(path);
    }

    private static void RunMigrations(SqliteConnectionFactory factory)
    {
        new MigrationRunner(factory, [new Migration001_InitialSchema()]).RunPending();
    }

    private static void SeedLegacyV1Database(SqliteConnectionFactory factory, string sessionId, string taskName)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS WorkSession (
                Id TEXT PRIMARY KEY NOT NULL,
                TaskName TEXT NOT NULL,
                Mode INTEGER NOT NULL,
                StartedAt TEXT NOT NULL,
                FinishedAt TEXT NULL,
                CountdownDurationTicks INTEGER NULL,
                State INTEGER NOT NULL,
                Note TEXT NULL,
                LastHeartbeatAt TEXT NULL,
                CountdownEndsAt TEXT NULL,
                CountdownRemainingTicks INTEGER NULL,
                CountdownCompletedNotified INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS WorkInterval (
                Id TEXT PRIMARY KEY NOT NULL,
                WorkSessionId TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT NULL,
                FOREIGN KEY (WorkSessionId) REFERENCES WorkSession(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_WorkInterval_Session
                ON WorkInterval(WorkSessionId);

            CREATE TABLE IF NOT EXISTS AppSetting (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL
            );

            INSERT INTO WorkSession (
                Id, TaskName, Mode, StartedAt, FinishedAt, CountdownDurationTicks,
                State, Note, LastHeartbeatAt, CountdownEndsAt, CountdownRemainingTicks,
                CountdownCompletedNotified
            ) VALUES (
                @id, @taskName, 0, '2026-01-01T00:00:00.0000000+00:00', NULL, NULL,
                0, NULL, NULL, NULL, NULL, 0
            );
            """;
        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@taskName", taskName);
        command.ExecuteNonQuery();
    }

    private static int GetSchemaVersion(SqliteConnectionFactory factory)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int CountSchemaVersionRows(SqliteConnectionFactory factory)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SchemaVersion;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool TableExists(SqliteConnectionFactory factory, string tableName)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name = @name;
            """;
        command.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static string GetTaskName(SqliteConnectionFactory factory, string sessionId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TaskName FROM WorkSession WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", sessionId);
        return (string)command.ExecuteScalar()!;
    }
}
