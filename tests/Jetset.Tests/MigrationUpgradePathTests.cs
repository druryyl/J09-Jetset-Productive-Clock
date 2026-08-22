using System.IO;
using Jetset.App.Models;
using Jetset.App.Persistence;
using Jetset.App.Persistence.Migrations;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.Tests;

/// <summary>
/// R-19: End-to-end upgrade path validation from V1 and draft V2 (001–007) to the aligned schema.
/// </summary>
public class MigrationUpgradePathTests : IDisposable
{
    private const int CurrentSchemaVersion = 12;

    private const int LegacyActive = 0;
    private const int LegacyBlocked = 1;
    private const int LegacyDone = 2;
    private const int LegacyCancelled = 3;

    private readonly string _tempDir;
    private readonly List<string> _dbPaths = [];

    static MigrationUpgradePathTests()
    {
        SQLitePCL.Batteries.Init();
    }

    public MigrationUpgradePathTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "JetsetMigrationUpgradeTests", Guid.NewGuid().ToString("N"));
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
    public void GivenLegacyV1Database_WhenUpgradedToCurrent_ThenSessionDataIsIntact()
    {
        var factory = CreateFactory();
        const string sessionId = "v1-session-1";
        const string intervalId1 = "v1-interval-1";
        const string intervalId2 = "v1-interval-2";
        const string taskName = "Ship feature";
        const string startedAt = "2026-01-15T09:00:00.0000000+00:00";
        const string finishedAt = "2026-01-15T10:30:00.0000000+00:00";
        const string note = "Morning focus block";
        const int mode = 1;
        const int state = (int)SessionState.Completed;

        SeedLegacyV1Database(factory);
        InsertLegacyV1Session(
            factory,
            sessionId,
            taskName,
            mode,
            state,
            startedAt,
            finishedAt,
            note);
        InsertLegacyV1Interval(factory, intervalId1, sessionId, "2026-01-15T09:00:00.0000000+00:00", "2026-01-15T09:45:00.0000000+00:00");
        InsertLegacyV1Interval(factory, intervalId2, sessionId, "2026-01-15T10:00:00.0000000+00:00", "2026-01-15T10:30:00.0000000+00:00");

        RunMigrations(factory);

        Assert.Equal(CurrentSchemaVersion, GetSchemaVersion(factory));
        Assert.Equal(1, CountSessions(factory));
        Assert.Equal(2, CountIntervals(factory));
        Assert.Equal(1, CountTasks(factory));

        var session = GetSession(factory, sessionId);
        Assert.Equal(taskName, session.TaskName);
        Assert.Equal(mode, session.Mode);
        Assert.Equal(state, session.State);
        Assert.Equal(startedAt, session.StartedAt);
        Assert.Equal(finishedAt, session.FinishedAt);
        Assert.Equal(note, session.Note);
        Assert.NotNull(session.TaskId);

        var task = GetTask(factory, session.TaskId!);
        Assert.Equal(taskName, task.Title);
        Assert.Equal((int)TaskStatus.Done, task.Status);

        Assert.Equal(2, CountIntervalsForSession(factory, sessionId));
        AssertInterval(factory, intervalId1, sessionId, "2026-01-15T09:00:00.0000000+00:00", "2026-01-15T09:45:00.0000000+00:00");
        AssertInterval(factory, intervalId2, sessionId, "2026-01-15T10:00:00.0000000+00:00", "2026-01-15T10:30:00.0000000+00:00");

        var validation = new MigrationValidationService().Validate(factory);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));
    }

    [Fact]
    public void GivenLegacyV1DatabaseWithMultipleSessions_WhenUpgradedToCurrent_ThenTasksAndLinksArePreserved()
    {
        var factory = CreateFactory();
        SeedLegacyV1Database(factory);
        InsertLegacyV1Session(factory, "v1-a", "Alpha", 0, (int)SessionState.Running, "2026-02-01T08:00:00.0000000+00:00", null, null);
        InsertLegacyV1Session(factory, "v1-b", "Alpha", 0, (int)SessionState.Completed, "2026-02-01T10:00:00.0000000+00:00", "2026-02-01T11:00:00.0000000+00:00", null);
        InsertLegacyV1Session(factory, "v1-c", "Beta", 0, (int)SessionState.Paused, "2026-02-02T08:00:00.0000000+00:00", null, null);

        RunMigrations(factory);

        Assert.Equal(3, CountSessions(factory));
        Assert.Equal(2, CountTasks(factory));

        var alphaTaskId = GetTaskIdForSession(factory, "v1-a");
        Assert.Equal(alphaTaskId, GetTaskIdForSession(factory, "v1-b"));
        Assert.NotEqual(alphaTaskId, GetTaskIdForSession(factory, "v1-c"));

        var validation = new MigrationValidationService().Validate(factory);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));
    }

    [Fact]
    public void GivenDraftV2DatabaseAtVersion7_WhenUpgradedToCurrent_ThenAlignedSchemaAndValidationPasses()
    {
        var factory = CreateFactory();
        var data = SeedDraftV2DatabaseAtVersion7(factory);

        RunMigrations(factory);

        Assert.Equal(CurrentSchemaVersion, GetSchemaVersion(factory));
        Assert.False(TableExists(factory, "Milestone"));
        Assert.False(TableExists(factory, "ContextSnapshot"));
        Assert.False(TableExists(factory, "TaskSwitchEvent"));
        Assert.False(ColumnExists(factory, "Task", "MilestoneId"));
        Assert.False(ColumnExists(factory, "Task", "CurrentStatus"));
        Assert.True(ColumnExists(factory, "Project", "ContextText"));

        Assert.Equal(data.SessionCount, CountSessions(factory));
        Assert.Equal(data.IntervalCount, CountIntervals(factory));
        Assert.Equal(data.TaskCount, CountTasks(factory));

        foreach (var sessionId in data.SessionIds)
        {
            var session = GetSession(factory, sessionId);
            Assert.NotNull(session.TaskId);
            Assert.Equal(session.TaskName, GetTask(factory, session.TaskId!).Title);
        }

        var validation = new MigrationValidationService().Validate(factory);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));
    }

    [Fact]
    public void GivenSchema11Database_WhenUpgradedToCurrent_ThenEstimateMinutesColumnExists()
    {
        var factory = CreateFactory();
        SeedDraftV2DatabaseAtVersion7(factory);

        RunMigrationsThroughVersion(factory, 11);
        Assert.Equal(11, GetSchemaVersion(factory));
        Assert.False(ColumnExists(factory, "Task", "EstimateMinutes"));

        RunMigrations(factory);

        Assert.Equal(CurrentSchemaVersion, GetSchemaVersion(factory));
        Assert.True(ColumnExists(factory, "Task", "EstimateMinutes"));

        var validation = new MigrationValidationService().Validate(factory);
        Assert.True(validation.IsValid, string.Join(" ", validation.Errors));
    }

    [Fact]
    public void GivenDraftV2DatabaseAtVersion7_WhenUpgraded_ThenStatusesAndContextAreRemapped()
    {
        var factory = CreateFactory();
        var data = SeedDraftV2DatabaseAtVersion7(factory);

        RunMigrations(factory);

        Assert.Equal((int)TaskStatus.Ready, GetTaskStatus(factory, data.ReadyTaskId));
        Assert.Equal((int)TaskStatus.Waiting, GetTaskStatus(factory, data.WaitingTaskId));
        Assert.Equal((int)TaskStatus.Done, GetTaskStatus(factory, data.DoneTaskId));
        Assert.NotNull(GetTaskCompletedAt(factory, data.DoneTaskId));
        Assert.Equal((int)TaskStatus.Cancelled, GetTaskStatus(factory, data.CancelledTaskId));
        Assert.Equal((int)TaskStatus.Running, GetTaskStatus(factory, data.NewestRunningTaskId));
        Assert.Equal((int)TaskStatus.Ready, GetTaskStatus(factory, data.OlderPausedTaskId));

        var context = GetProjectContext(factory, data.ProjectId);
        Assert.Contains("Current: Active work", context.ContextText, StringComparison.Ordinal);
        Assert.Contains("Progress: Half done", context.ContextText, StringComparison.Ordinal);
        Assert.Contains("Next: Finish migration", context.ContextText, StringComparison.Ordinal);
        Assert.Contains("Blocker: Waiting on review", context.ContextText, StringComparison.Ordinal);
        Assert.NotNull(context.ContextUpdatedAt);
    }

    [Fact]
    public void GivenDraftV2DatabaseAtVersion7_WhenUpgraded_ThenDeprecatedArtifactsAreRemovedButTasksRemain()
    {
        var factory = CreateFactory();
        var data = SeedDraftV2DatabaseAtVersion7(factory);

        RunMigrations(factory);

        Assert.False(TableExists(factory, "Milestone"));
        Assert.False(TableExists(factory, "ContextSnapshot"));
        Assert.False(TableExists(factory, "TaskSwitchEvent"));
        Assert.Equal(1, CountRows(factory, "Project", $"Id = '{data.ProjectId}'"));
        Assert.Equal(data.TaskCount, CountRows(factory, "Task"));
    }

    [Fact]
    public void GivenMultipleRunningTasksAfterMigration_WhenValidated_ThenFails()
    {
        var factory = CreateFactory();
        RunMigrations(factory);

        var taskA = Guid.NewGuid().ToString();
        var taskB = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToString("O");

        using (var connection = factory.Create())
        {
            foreach (var taskId in new[] { taskA, taskB })
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO "Task" (Id, Title, Status, Notes, ProjectId, CreatedAt, UpdatedAt, Origin, CompletedAt)
                    VALUES (@id, @title, @status, NULL, NULL, @now, @now, 0, NULL);
                    """;
                command.Parameters.AddWithValue("@id", taskId);
                command.Parameters.AddWithValue("@title", "Task " + taskId[..8]);
                command.Parameters.AddWithValue("@status", (int)TaskStatus.Running);
                command.Parameters.AddWithValue("@now", now);
                command.ExecuteNonQuery();
            }
        }

        var result = new MigrationValidationService().Validate(factory);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Running", StringComparison.OrdinalIgnoreCase));
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

    private static void RunMigrationsThroughVersion(SqliteConnectionFactory factory, int targetVersion)
    {
        var migrations = new IMigration[]
        {
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable(),
            new Migration004_AddMilestoneTable(),
            new Migration005_AddContextSnapshotTable(),
            new Migration006_AddWorkSessionTaskId(),
            new Migration007_AddTaskSwitchEventTable(),
            new Migration008_TaskLifecycleRealignment(),
            new Migration009_AddProjectContextText(),
            new Migration010_MigrateTaskContextToProject(),
            new Migration011_SchemaCleanup(),
            new Migration012_AddTaskEstimateMinutes()
        };

        var selected = migrations.Where(m => m.Version <= targetVersion).ToArray();
        new MigrationRunner(factory, selected).RunPending();
    }

    private static void SeedLegacyV1Database(SqliteConnectionFactory factory)
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
            """;
        command.ExecuteNonQuery();
    }

    private static void InsertLegacyV1Session(
        SqliteConnectionFactory factory,
        string sessionId,
        string taskName,
        int mode,
        int state,
        string startedAt,
        string? finishedAt,
        string? note)
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
                @id, @taskName, @mode, @startedAt, @finishedAt, NULL,
                @state, @note, NULL, NULL, NULL, 0
            );
            """;
        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@taskName", taskName);
        command.Parameters.AddWithValue("@mode", mode);
        command.Parameters.AddWithValue("@startedAt", startedAt);
        command.Parameters.AddWithValue("@finishedAt", (object?)finishedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@state", state);
        command.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void InsertLegacyV1Interval(
        SqliteConnectionFactory factory,
        string intervalId,
        string sessionId,
        string startedAt,
        string? endedAt)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO WorkInterval (Id, WorkSessionId, StartedAt, EndedAt)
            VALUES (@id, @sessionId, @startedAt, @endedAt);
            """;
        command.Parameters.AddWithValue("@id", intervalId);
        command.Parameters.AddWithValue("@sessionId", sessionId);
        command.Parameters.AddWithValue("@startedAt", startedAt);
        command.Parameters.AddWithValue("@endedAt", (object?)endedAt ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private sealed record DraftV2SeedData(
        string ProjectId,
        string ReadyTaskId,
        string WaitingTaskId,
        string DoneTaskId,
        string CancelledTaskId,
        string NewestRunningTaskId,
        string OlderPausedTaskId,
        IReadOnlyList<string> SessionIds,
        int SessionCount,
        int IntervalCount,
        int TaskCount);

    private static DraftV2SeedData SeedDraftV2DatabaseAtVersion7(SqliteConnectionFactory factory)
    {
        new MigrationRunner(factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable(),
            new Migration004_AddMilestoneTable(),
            new Migration005_AddContextSnapshotTable(),
            new Migration006_AddWorkSessionTaskId(),
            new Migration007_AddTaskSwitchEventTable()
        ]).RunPending();

        var projectId = "draft-project-1";
        var milestoneId = "draft-milestone-1";
        var readyTaskId = "draft-task-ready";
        var waitingTaskId = "draft-task-waiting";
        var doneTaskId = "draft-task-done";
        var cancelledTaskId = "draft-task-cancelled";
        var newestRunningTaskId = "draft-task-running-new";
        var olderPausedTaskId = "draft-task-paused-old";
        var snapshotId = "draft-snapshot-1";
        var switchEventId = "draft-switch-1";
        var sessionRunningId = "draft-session-running";
        var sessionPausedId = "draft-session-paused";
        var sessionDoneId = "draft-session-done";
        var intervalRunningId = "draft-interval-running";
        var intervalPausedId = "draft-interval-paused";
        var intervalDoneId = "draft-interval-done";

        var createdAt = "2026-01-01T00:00:00.0000000+00:00";
        var doneUpdatedAt = "2026-01-10T00:00:00.0000000+00:00";
        var contextUpdatedAt = "2026-02-02T12:00:00.0000000+00:00";
        var olderStartedAt = "2026-02-01T08:00:00.0000000+00:00";
        var newerStartedAt = "2026-02-02T09:00:00.0000000+00:00";
        var doneStartedAt = "2026-01-05T09:00:00.0000000+00:00";
        var doneFinishedAt = "2026-01-05T10:00:00.0000000+00:00";

        using (var connection = factory.Create())
        {
            InsertProject(connection, projectId, "Draft project", createdAt);
            InsertMilestone(connection, milestoneId, projectId, "Release", createdAt);
            InsertDraftTask(connection, readyTaskId, "Ready task", LegacyActive, projectId, milestoneId, createdAt, createdAt, null, null, null, null);
            InsertDraftTask(connection, waitingTaskId, "Waiting task", LegacyBlocked, projectId, null, createdAt, createdAt, null, null, null, null);
            InsertDraftTask(connection, doneTaskId, "Done task", LegacyDone, null, null, createdAt, doneUpdatedAt, null, null, null, null);
            InsertDraftTask(connection, cancelledTaskId, "Cancelled task", LegacyCancelled, null, null, createdAt, createdAt, null, null, null, null);
            InsertDraftTask(
                connection,
                newestRunningTaskId,
                "Running task",
                LegacyActive,
                projectId,
                milestoneId,
                createdAt,
                contextUpdatedAt,
                "Active work",
                "Half done",
                "Finish migration",
                "Waiting on review");
            InsertDraftTask(connection, olderPausedTaskId, "Paused task", LegacyActive, projectId, null, createdAt, olderStartedAt, null, null, null, null);

            InsertLinkedSession(connection, sessionRunningId, newestRunningTaskId, "Running task", (int)SessionState.Running, newerStartedAt, null);
            InsertLinkedSession(connection, sessionPausedId, olderPausedTaskId, "Paused task", (int)SessionState.Paused, olderStartedAt, null);
            InsertLinkedSession(connection, sessionDoneId, doneTaskId, "Done task", (int)SessionState.Completed, doneStartedAt, doneFinishedAt);

            InsertInterval(connection, intervalRunningId, sessionRunningId, newerStartedAt, null);
            InsertInterval(connection, intervalPausedId, sessionPausedId, olderStartedAt, "2026-02-01T08:30:00.0000000+00:00");
            InsertInterval(connection, intervalDoneId, sessionDoneId, doneStartedAt, doneFinishedAt);

            InsertSnapshot(connection, snapshotId, newestRunningTaskId, contextUpdatedAt);
            InsertSwitchEvent(connection, switchEventId, olderPausedTaskId, newestRunningTaskId, newerStartedAt);
        }

        return new DraftV2SeedData(
            projectId,
            readyTaskId,
            waitingTaskId,
            doneTaskId,
            cancelledTaskId,
            newestRunningTaskId,
            olderPausedTaskId,
            [sessionRunningId, sessionPausedId, sessionDoneId],
            SessionCount: 3,
            IntervalCount: 3,
            TaskCount: 6);
    }

    private static void InsertProject(Microsoft.Data.Sqlite.SqliteConnection connection, string id, string name, string createdAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Project (Id, Name, Deadline, CreatedAt, UpdatedAt)
            VALUES (@id, @name, NULL, @createdAt, @updatedAt);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@createdAt", createdAt);
        command.Parameters.AddWithValue("@updatedAt", createdAt);
        command.ExecuteNonQuery();
    }

    private static void InsertMilestone(Microsoft.Data.Sqlite.SqliteConnection connection, string id, string projectId, string name, string createdAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Milestone (Id, ProjectId, Name, SortOrder, CreatedAt)
            VALUES (@id, @projectId, @name, 0, @createdAt);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@projectId", projectId);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@createdAt", createdAt);
        command.ExecuteNonQuery();
    }

    private static void InsertDraftTask(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string id,
        string title,
        int status,
        string? projectId,
        string? milestoneId,
        string createdAt,
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
                ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt)
            VALUES (
                @id, @title, @status, NULL, @currentStatus, @lastProgress, @nextAction, @blocker,
                @projectId, @milestoneId, @createdAt, @updatedAt, NULL);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@currentStatus", (object?)currentStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastProgress", (object?)lastProgress ?? DBNull.Value);
        command.Parameters.AddWithValue("@nextAction", (object?)nextAction ?? DBNull.Value);
        command.Parameters.AddWithValue("@blocker", (object?)blocker ?? DBNull.Value);
        command.Parameters.AddWithValue("@projectId", (object?)projectId ?? DBNull.Value);
        command.Parameters.AddWithValue("@milestoneId", (object?)milestoneId ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", createdAt);
        command.Parameters.AddWithValue("@updatedAt", updatedAt);
        command.ExecuteNonQuery();
    }

    private static void InsertLinkedSession(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string sessionId,
        string taskId,
        string taskName,
        int state,
        string startedAt,
        string? finishedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO WorkSession (
                Id, TaskName, Mode, StartedAt, FinishedAt, CountdownDurationTicks,
                State, Note, LastHeartbeatAt, CountdownEndsAt, CountdownRemainingTicks,
                CountdownCompletedNotified, TaskId)
            VALUES (
                @id, @taskName, 0, @startedAt, @finishedAt, NULL,
                @state, NULL, NULL, NULL, NULL, 0, @taskId);
            """;
        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@taskName", taskName);
        command.Parameters.AddWithValue("@startedAt", startedAt);
        command.Parameters.AddWithValue("@finishedAt", (object?)finishedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@state", state);
        command.Parameters.AddWithValue("@taskId", taskId);
        command.ExecuteNonQuery();
    }

    private static void InsertInterval(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string intervalId,
        string sessionId,
        string startedAt,
        string? endedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO WorkInterval (Id, WorkSessionId, StartedAt, EndedAt)
            VALUES (@id, @sessionId, @startedAt, @endedAt);
            """;
        command.Parameters.AddWithValue("@id", intervalId);
        command.Parameters.AddWithValue("@sessionId", sessionId);
        command.Parameters.AddWithValue("@startedAt", startedAt);
        command.Parameters.AddWithValue("@endedAt", (object?)endedAt ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void InsertSnapshot(Microsoft.Data.Sqlite.SqliteConnection connection, string id, string taskId, string createdAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ContextSnapshot (
                Id, TaskId, CreatedAt, CurrentStatus, LastProgress, NextAction, Blocker, Notes)
            VALUES (@id, @taskId, @createdAt, 'Snapshot status', 'Snapshot progress', 'Snapshot next', 'Snapshot blocker', NULL);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@taskId", taskId);
        command.Parameters.AddWithValue("@createdAt", createdAt);
        command.ExecuteNonQuery();
    }

    private static void InsertSwitchEvent(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string id,
        string fromTaskId,
        string toTaskId,
        string occurredAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO TaskSwitchEvent (Id, FromTaskId, ToTaskId, OccurredAt)
            VALUES (@id, @fromTaskId, @toTaskId, @occurredAt);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@fromTaskId", fromTaskId);
        command.Parameters.AddWithValue("@toTaskId", toTaskId);
        command.Parameters.AddWithValue("@occurredAt", occurredAt);
        command.ExecuteNonQuery();
    }

    private static int GetSchemaVersion(SqliteConnectionFactory factory)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
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

    private static int CountSessions(SqliteConnectionFactory factory) =>
        CountRows(factory, "WorkSession");

    private static int CountIntervals(SqliteConnectionFactory factory) =>
        CountRows(factory, "WorkInterval");

    private static int CountTasks(SqliteConnectionFactory factory) =>
        CountRows(factory, "Task");

    private static int CountRows(SqliteConnectionFactory factory, string table, string? whereClause = null)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = whereClause is null
            ? $"SELECT COUNT(*) FROM {QuoteTable(table)};"
            : $"SELECT COUNT(*) FROM {QuoteTable(table)} WHERE {whereClause};";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string QuoteTable(string table) =>
        table == "Task" ? "\"Task\"" : table;

    private sealed record SessionRow(
        string TaskName,
        int Mode,
        int State,
        string StartedAt,
        string? FinishedAt,
        string? Note,
        string? TaskId);

    private static SessionRow GetSession(SqliteConnectionFactory factory, string sessionId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT TaskName, Mode, State, StartedAt, FinishedAt, Note, TaskId
            FROM WorkSession
            WHERE Id = @id;
            """;
        command.Parameters.AddWithValue("@id", sessionId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return new SessionRow(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private sealed record TaskRow(string Title, int Status);

    private static TaskRow GetTask(SqliteConnectionFactory factory, string taskId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Title, Status FROM \"Task\" WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", taskId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return new TaskRow(reader.GetString(0), reader.GetInt32(1));
    }

    private static int GetTaskStatus(SqliteConnectionFactory factory, string taskId) =>
        GetTask(factory, taskId).Status;

    private static string? GetTaskCompletedAt(SqliteConnectionFactory factory, string taskId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CompletedAt FROM \"Task\" WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", taskId);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : (string)result;
    }

    private static string? GetTaskIdForSession(SqliteConnectionFactory factory, string sessionId) =>
        GetSession(factory, sessionId).TaskId;

    private static int CountIntervalsForSession(SqliteConnectionFactory factory, string sessionId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM WorkInterval WHERE WorkSessionId = @sessionId;";
        command.Parameters.AddWithValue("@sessionId", sessionId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void AssertInterval(
        SqliteConnectionFactory factory,
        string intervalId,
        string sessionId,
        string startedAt,
        string? endedAt)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT WorkSessionId, StartedAt, EndedAt
            FROM WorkInterval
            WHERE Id = @id;
            """;
        command.Parameters.AddWithValue("@id", intervalId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(sessionId, reader.GetString(0));
        Assert.Equal(startedAt, reader.GetString(1));
        if (endedAt is null)
        {
            Assert.True(reader.IsDBNull(2));
        }
        else
        {
            Assert.Equal(endedAt, reader.GetString(2));
        }
    }

    private sealed record ProjectContextRow(string? ContextText, string? ContextUpdatedAt);

    private static ProjectContextRow GetProjectContext(SqliteConnectionFactory factory, string projectId)
    {
        using var connection = factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ContextText, ContextUpdatedAt FROM Project WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", projectId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return new ProjectContextRow(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }
}
