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
    public void GivenFreshDatabase_WhenRunnerRuns_ThenVersionIs11AndCoreTablesExist()
    {
        var factory = CreateFactory();

        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.True(TableExists(factory, "WorkSession"));
        Assert.True(TableExists(factory, "WorkInterval"));
        Assert.True(TableExists(factory, "AppSetting"));
        Assert.True(TableExists(factory, "SchemaVersion"));
        Assert.True(TableExists(factory, "Task"));
        Assert.True(TableExists(factory, "Project"));
        Assert.False(TableExists(factory, "Milestone"));
        Assert.False(TableExists(factory, "ContextSnapshot"));
        Assert.False(TableExists(factory, "TaskSwitchEvent"));
        Assert.False(ColumnExists(factory, "Task", "MilestoneId"));
    }

    [Fact]
    public void GivenFreshDatabase_WhenRunnerRunsTwice_ThenIsIdempotent()
    {
        var factory = CreateFactory();

        RunMigrations(factory);
        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.Equal(11, CountSchemaVersionRows(factory));
    }

    [Fact]
    public void GivenLegacyV1Database_WhenRunnerRuns_ThenDataPreservedAndVersionRecorded()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, sessionId: "legacy-session-1", taskName: "Legacy Task");

        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.Equal("Legacy Task", GetTaskName(factory, "legacy-session-1"));
        Assert.Equal("Legacy Task", GetTaskTitleForSession(factory, "legacy-session-1"));
        Assert.True(TableExists(factory, "Task"));
        Assert.True(TableExists(factory, "Project"));
        Assert.False(TableExists(factory, "Milestone"));
        Assert.False(TableExists(factory, "ContextSnapshot"));
    }

    [Fact]
    public void GivenLegacyV1Database_WhenRunnerRunsTwice_ThenIsIdempotent()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, sessionId: "legacy-session-2", taskName: "Still Here");

        RunMigrations(factory);
        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.Equal(11, CountSchemaVersionRows(factory));
        Assert.Equal("Still Here", GetTaskName(factory, "legacy-session-2"));
    }

    [Fact]
    public void GivenV2Database_WhenRunnerRuns_ThenProjectMilestoneAndSnapshotTablesAdded()
    {
        var factory = CreateFactory();
        SeedV2Database(factory);

        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.True(TableExists(factory, "Project"));
        Assert.True(TableExists(factory, "Task"));
        Assert.False(TableExists(factory, "Milestone"));
        Assert.False(TableExists(factory, "ContextSnapshot"));
    }

    [Fact]
    public void GivenV3Database_WhenRunnerRuns_ThenMilestoneAndSnapshotTablesAdded()
    {
        var factory = CreateFactory();
        SeedV3Database(factory);

        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.False(TableExists(factory, "Milestone"));
        Assert.False(TableExists(factory, "ContextSnapshot"));
        Assert.True(TableExists(factory, "Project"));
    }

    [Fact]
    public void GivenV4Database_WhenRunnerRuns_ThenContextSnapshotTableAdded()
    {
        var factory = CreateFactory();
        SeedV4Database(factory);

        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.False(TableExists(factory, "ContextSnapshot"));
        Assert.False(TableExists(factory, "Milestone"));
    }

    [Fact]
    public void GivenV5Database_WhenRunnerRuns_ThenWorkSessionTaskIdBackfilled()
    {
        var factory = CreateFactory();
        SeedV5Database(factory);

        RunMigrations(factory);

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.NotNull(GetTaskId(factory, "v5-session-1"));
        Assert.Equal("V5 Task", GetTaskTitleForSession(factory, "v5-session-1"));
    }

    [Fact]
    public void GivenLegacyV1Database_WhenRunnerRuns_ThenUpgradedFromV1FlagIsSet()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory, sessionId: "legacy-session-3", taskName: "Migrated Task");

        RunMigrations(factory);

        Assert.Equal("true", GetAppSetting(factory, "UpgradedFromV1"));
    }

    [Fact]
    public void GivenProjectWithTaskContext_WhenMigration010Runs_ThenContextTextIsMigrated()
    {
        var factory = CreateFactory();
        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable(),
            new Migration004_AddMilestoneTable(),
            new Migration005_AddContextSnapshotTable(),
            new Migration006_AddWorkSessionTaskId(),
            new Migration007_AddTaskSwitchEventTable(),
            new Migration008_TaskLifecycleRealignment(),
            new Migration009_AddProjectContextText()
        ]).RunPending();

        var projectId = Guid.NewGuid().ToString();
        var olderTaskId = Guid.NewGuid().ToString();
        var newerTaskId = Guid.NewGuid().ToString();
        var now = "2026-08-22T10:00:00.0000000+00:00";
        var later = "2026-08-22T12:00:00.0000000+00:00";

        using (var connection = factory.Create())
        {
            using var project = connection.CreateCommand();
            project.CommandText =
                """
                INSERT INTO Project (Id, Name, Deadline, CreatedAt, UpdatedAt)
                VALUES (@id, 'Migration project', NULL, @createdAt, @updatedAt);
                """;
            project.Parameters.AddWithValue("@id", projectId);
            project.Parameters.AddWithValue("@createdAt", now);
            project.Parameters.AddWithValue("@updatedAt", now);
            project.ExecuteNonQuery();

            InsertTaskWithContext(connection, olderTaskId, projectId, now, "Old status", null, null, null);
            InsertTaskWithContext(
                connection,
                newerTaskId,
                projectId,
                later,
                "Current status",
                "Made progress",
                "Ship feature",
                "Waiting on review");
        }

        new MigrationRunner(factory, [new Migration010_MigrateTaskContextToProject()]).RunPending();

        using (var connection = factory.Create())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ContextText, ContextUpdatedAt FROM Project WHERE Id = @id;";
            command.Parameters.AddWithValue("@id", projectId);
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            var contextText = reader.GetString(0);
            Assert.Contains("Current: Current status", contextText, StringComparison.Ordinal);
            Assert.Contains("Progress: Made progress", contextText, StringComparison.Ordinal);
            Assert.Contains("Next: Ship feature", contextText, StringComparison.Ordinal);
            Assert.Contains("Blocker: Waiting on review", contextText, StringComparison.Ordinal);
            Assert.DoesNotContain("Old status", contextText, StringComparison.Ordinal);
            Assert.Equal(later, reader.GetString(1));
        }

        Assert.False(ColumnExists(factory, "Task", "CurrentStatus"));
        Assert.False(ColumnExists(factory, "Task", "LastProgress"));
        Assert.False(ColumnExists(factory, "Task", "NextAction"));
        Assert.False(ColumnExists(factory, "Task", "Blocker"));
    }

    [Fact]
    public void GivenDraftV2Database_WhenMigration011Runs_ThenDeprecatedSchemaIsRemoved()
    {
        var factory = CreateFactory();
        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable(),
            new Migration004_AddMilestoneTable(),
            new Migration005_AddContextSnapshotTable(),
            new Migration006_AddWorkSessionTaskId(),
            new Migration007_AddTaskSwitchEventTable(),
            new Migration008_TaskLifecycleRealignment(),
            new Migration009_AddProjectContextText(),
            new Migration010_MigrateTaskContextToProject()
        ]).RunPending();

        var projectId = Guid.NewGuid().ToString();
        var milestoneId = Guid.NewGuid().ToString();
        var taskId = Guid.NewGuid().ToString();
        var snapshotId = Guid.NewGuid().ToString();
        var switchEventId = Guid.NewGuid().ToString();
        var now = "2026-08-22T10:00:00.0000000+00:00";

        using (var connection = factory.Create())
        {
            using var project = connection.CreateCommand();
            project.CommandText =
                """
                INSERT INTO Project (Id, Name, Deadline, CreatedAt, UpdatedAt, ContextText, ContextUpdatedAt)
                VALUES (@id, 'Cleanup project', NULL, @now, @now, 'Existing context', @now);
                """;
            project.Parameters.AddWithValue("@id", projectId);
            project.Parameters.AddWithValue("@now", now);
            project.ExecuteNonQuery();

            using var milestone = connection.CreateCommand();
            milestone.CommandText =
                """
                INSERT INTO Milestone (Id, ProjectId, Name, SortOrder, CreatedAt)
                VALUES (@id, @projectId, 'Milestone', 0, @now);
                """;
            milestone.Parameters.AddWithValue("@id", milestoneId);
            milestone.Parameters.AddWithValue("@projectId", projectId);
            milestone.Parameters.AddWithValue("@now", now);
            milestone.ExecuteNonQuery();

            using var task = connection.CreateCommand();
            task.CommandText =
                """
                INSERT INTO "Task" (
                    Id, Title, Status, Notes, ProjectId, MilestoneId,
                    CreatedAt, UpdatedAt, LastWorkedAt, Origin, CompletedAt)
                VALUES (
                    @id, 'Cleanup task', 1, NULL, @projectId, @milestoneId,
                    @now, @now, NULL, 0, NULL);
                """;
            task.Parameters.AddWithValue("@id", taskId);
            task.Parameters.AddWithValue("@projectId", projectId);
            task.Parameters.AddWithValue("@milestoneId", milestoneId);
            task.Parameters.AddWithValue("@now", now);
            task.ExecuteNonQuery();

            using var snapshot = connection.CreateCommand();
            snapshot.CommandText =
                """
                INSERT INTO ContextSnapshot (
                    Id, TaskId, CreatedAt, CurrentStatus, LastProgress, NextAction, Blocker, Notes)
                VALUES (@id, @taskId, @now, 'Status', 'Progress', 'Next', 'Blocker', NULL);
                """;
            snapshot.Parameters.AddWithValue("@id", snapshotId);
            snapshot.Parameters.AddWithValue("@taskId", taskId);
            snapshot.Parameters.AddWithValue("@now", now);
            snapshot.ExecuteNonQuery();

            using var switchEvent = connection.CreateCommand();
            switchEvent.CommandText =
                """
                INSERT INTO TaskSwitchEvent (Id, FromTaskId, ToTaskId, OccurredAt)
                VALUES (@id, NULL, @taskId, @now);
                """;
            switchEvent.Parameters.AddWithValue("@id", switchEventId);
            switchEvent.Parameters.AddWithValue("@taskId", taskId);
            switchEvent.Parameters.AddWithValue("@now", now);
            switchEvent.ExecuteNonQuery();
        }

        new MigrationRunner(factory, [new Migration011_SchemaCleanup()]).RunPending();

        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.False(TableExists(factory, "Milestone"));
        Assert.False(TableExists(factory, "ContextSnapshot"));
        Assert.False(TableExists(factory, "TaskSwitchEvent"));
        Assert.False(ColumnExists(factory, "Task", "MilestoneId"));
        Assert.False(ColumnExists(factory, "Task", "CurrentStatus"));
        Assert.False(ColumnExists(factory, "Task", "LastProgress"));
        Assert.False(ColumnExists(factory, "Task", "NextAction"));
        Assert.False(ColumnExists(factory, "Task", "Blocker"));

        using (var connection = factory.Create())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"Task\" WHERE Id = @id;";
            command.Parameters.AddWithValue("@id", taskId);
            Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
        }

        var validation = new MigrationValidationService().Validate(factory);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));
    }

    private static void InsertTaskWithContext(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string taskId,
        string projectId,
        string updatedAt,
        string? currentStatus,
        string? lastProgress,
        string? nextAction,
        string? blocker)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "Task" (
                Id, Title, Status, Notes, CurrentStatus, LastProgress, NextAction, Blocker,
                ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt, Origin, CompletedAt)
            VALUES (
                @id, @title, 1, NULL, @currentStatus, @lastProgress, @nextAction, @blocker,
                @projectId, NULL, @createdAt, @updatedAt, NULL, 0, NULL);
            """;
        command.Parameters.AddWithValue("@id", taskId);
        command.Parameters.AddWithValue("@title", "Task " + taskId[..8]);
        command.Parameters.AddWithValue("@currentStatus", (object?)currentStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastProgress", (object?)lastProgress ?? DBNull.Value);
        command.Parameters.AddWithValue("@nextAction", (object?)nextAction ?? DBNull.Value);
        command.Parameters.AddWithValue("@blocker", (object?)blocker ?? DBNull.Value);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@createdAt", updatedAt);
        command.Parameters.AddWithValue("@updatedAt", updatedAt);
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnectionFactory factory, string table, string column)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM pragma_table_info(@table)
            WHERE name = @column;
            """;
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@column", column);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
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

    private static void SeedV2Database(SqliteConnectionFactory factory)
    {
        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable()
        ]).RunPending();
    }

    private static void SeedV3Database(SqliteConnectionFactory factory)
    {
        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable()
        ]).RunPending();
    }

    private static void SeedV4Database(SqliteConnectionFactory factory)
    {
        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable(),
            new Migration004_AddMilestoneTable()
        ]).RunPending();
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

    private static void SeedV5Database(SqliteConnectionFactory factory)
    {
        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable(),
            new Migration004_AddMilestoneTable(),
            new Migration005_AddContextSnapshotTable()
        ]).RunPending();

        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO WorkSession (
                Id, TaskName, Mode, StartedAt, FinishedAt, CountdownDurationTicks,
                State, Note, LastHeartbeatAt, CountdownEndsAt, CountdownRemainingTicks,
                CountdownCompletedNotified
            ) VALUES (
                @id, @taskName, 0, '2026-01-01T00:00:00.0000000+00:00', NULL, NULL,
                2, NULL, NULL, NULL, NULL, 0
            );
            """;
        command.Parameters.AddWithValue("@id", "v5-session-1");
        command.Parameters.AddWithValue("@taskName", "V5 Task");
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

    private static string? GetTaskId(SqliteConnectionFactory factory, string sessionId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TaskId FROM WorkSession WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", sessionId);
        var result = command.ExecuteScalar();
        return result is null || result is DBNull ? null : (string)result;
    }

    private static string GetTaskTitleForSession(SqliteConnectionFactory factory, string sessionId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT t.Title
            FROM WorkSession s
            INNER JOIN "Task" t ON t.Id = s.TaskId
            WHERE s.Id = @id;
            """;
        command.Parameters.AddWithValue("@id", sessionId);
        return (string)command.ExecuteScalar()!;
    }

    private static string? GetAppSetting(SqliteConnectionFactory factory, string key)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSetting WHERE Key = @key;";
        command.Parameters.AddWithValue("@key", key);
        var result = command.ExecuteScalar();
        return result is null || result is DBNull ? null : (string)result;
    }
}
