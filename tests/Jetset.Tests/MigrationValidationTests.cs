using System.IO;
using Jetset.App.Persistence;
using Jetset.App.Persistence.Migrations;

namespace Jetset.Tests;

public class MigrationValidationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static MigrationValidationTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public MigrationValidationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetMigrationValidationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var path in _dbPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void GivenMigratedDatabase_WhenValidated_ThenPasses()
    {
        var factory = CreateFactory();
        RunMigrations(factory);

        var result = new MigrationValidationService().Validate(factory);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GivenLegacyV1Database_WhenMigratedAndValidated_ThenPasses()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, "legacy-1", "Alpha");
        SeedLegacyV1Session(factory, "legacy-2", "Alpha");
        SeedLegacyV1Session(factory, "legacy-3", "Beta");

        RunMigrations(factory);

        var result = new MigrationValidationService().Validate(factory);
        Assert.True(result.IsValid);
        Assert.Equal(2, CountTasks(factory));
        Assert.NotNull(GetTaskId(factory, "legacy-1"));
        Assert.Equal(GetTaskId(factory, "legacy-1"), GetTaskId(factory, "legacy-2"));
        Assert.NotEqual(GetTaskId(factory, "legacy-1"), GetTaskId(factory, "legacy-3"));
    }

    [Fact]
    public void GivenSessionWithoutTaskId_WhenValidated_ThenFails()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, "invalid-session", "Broken");
        RunMigrations(factory);

        using (var connection = factory.Create())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE WorkSession SET TaskId = NULL;";
            command.ExecuteNonQuery();
        }

        var result = new MigrationValidationService().Validate(factory);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no linked task", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GivenUpgradingDatabase_WhenMigrationsRun_ThenBackupFileIsCreated()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, "legacy-backup", "Backup Task");

        RunMigrations(factory);

        var directory = Path.GetDirectoryName(factory.DatabasePath)!;
        var backupFiles = Directory.GetFiles(directory, $"{Path.GetFileName(factory.DatabasePath)}.backup-*");
        Assert.NotEmpty(backupFiles);
    }

    private SqliteConnectionFactory CreateFactory()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(path);
        return new SqliteConnectionFactory(path);
    }

    private static void RunMigrations(SqliteConnectionFactory factory)
    {
        new SchemaInitializer(factory).Initialize();
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

    private static void SeedLegacyV1Session(SqliteConnectionFactory factory, string sessionId, string taskName)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO WorkSession (
                Id, TaskName, Mode, StartedAt, FinishedAt, CountdownDurationTicks,
                State, Note, LastHeartbeatAt, CountdownEndsAt, CountdownRemainingTicks,
                CountdownCompletedNotified
            ) VALUES (
                @id, @taskName, 0, '2026-01-02T00:00:00.0000000+00:00', '2026-01-02T01:00:00.0000000+00:00', NULL,
                2, NULL, NULL, NULL, NULL, 0
            );
            """;
        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@taskName", taskName);
        command.ExecuteNonQuery();
    }

    private static int CountTasks(SqliteConnectionFactory factory)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"Task\";";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string? GetTaskId(SqliteConnectionFactory factory, string sessionId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TaskId FROM WorkSession WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", sessionId);
        var result = command.ExecuteScalar();
        return result is null || result is DBNull ? null : (string)result;
    }
}
