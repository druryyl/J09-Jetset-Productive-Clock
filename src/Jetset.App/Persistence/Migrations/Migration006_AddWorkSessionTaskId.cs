using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration006_AddWorkSessionTaskId : IMigration
{
    public int Version => 6;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (var addColumn = connection.CreateCommand())
        {
            addColumn.Transaction = transaction;
            addColumn.CommandText = "ALTER TABLE WorkSession ADD COLUMN TaskId TEXT NULL;";
            addColumn.ExecuteNonQuery();
        }

        BackfillTasksAndLinkSessions(connection, transaction);

        using (var index = connection.CreateCommand())
        {
            index.Transaction = transaction;
            index.CommandText =
                """
                CREATE INDEX IF NOT EXISTS IX_WorkSession_TaskId
                    ON WorkSession(TaskId);
                """;
            index.ExecuteNonQuery();
        }
    }

    private static void BackfillTasksAndLinkSessions(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText =
            """
            SELECT TaskName,
                   MIN(StartedAt) AS FirstStarted,
                   MAX(StartedAt) AS LastStarted,
                   SUM(CASE WHEN State IN (@running, @paused) THEN 1 ELSE 0 END) AS InProgressCount,
                   SUM(CASE WHEN State = @completed THEN 1 ELSE 0 END) AS CompletedCount,
                   SUM(CASE WHEN State = @cancelled THEN 1 ELSE 0 END) AS CancelledCount
            FROM WorkSession
            GROUP BY TaskName;
            """;
        select.Parameters.AddWithValue("@running", (int)SessionState.Running);
        select.Parameters.AddWithValue("@paused", (int)SessionState.Paused);
        select.Parameters.AddWithValue("@completed", (int)SessionState.Completed);
        select.Parameters.AddWithValue("@cancelled", (int)SessionState.Cancelled);

        var taskIdsByName = new Dictionary<string, string>();

        using (var reader = select.ExecuteReader())
        {
            while (reader.Read())
            {
                var taskName = reader.GetString(0);
                var firstStarted = DateTimeOffset.Parse(
                    reader.GetString(1),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
                var lastStarted = DateTimeOffset.Parse(
                    reader.GetString(2),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
                var inProgress = reader.GetInt64(3);
                var completed = reader.GetInt64(4);
                var cancelled = reader.GetInt64(5);

                var status = inProgress > 0
                    ? Models.TaskStatus.Active
                    : completed > 0
                        ? Models.TaskStatus.Done
                        : cancelled > 0
                            ? Models.TaskStatus.Cancelled
                            : Models.TaskStatus.Active;

                var existingTaskId = GetExistingTaskId(connection, transaction, taskName);
                var taskId = existingTaskId ?? Guid.NewGuid().ToString();
                taskIdsByName[taskName] = taskId;

                if (existingTaskId is null)
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText =
                        """
                        INSERT INTO "Task" (
                            Id, Title, Status, Notes, CurrentStatus, LastProgress, NextAction, Blocker,
                            ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt)
                        VALUES (
                            @id, @title, @status, NULL, NULL, NULL, NULL, NULL,
                            NULL, NULL, @createdAt, @updatedAt, @lastWorkedAt);
                        """;
                    insert.Parameters.AddWithValue("@id", taskId);
                    insert.Parameters.AddWithValue("@title", taskName);
                    insert.Parameters.AddWithValue("@status", (int)status);
                    insert.Parameters.AddWithValue("@createdAt", firstStarted.ToString("O", CultureInfo.InvariantCulture));
                    insert.Parameters.AddWithValue("@updatedAt", lastStarted.ToString("O", CultureInfo.InvariantCulture));
                    insert.Parameters.AddWithValue(
                        "@lastWorkedAt",
                        inProgress > 0 || completed > 0
                            ? lastStarted.ToString("O", CultureInfo.InvariantCulture)
                            : DBNull.Value);
                    insert.ExecuteNonQuery();
                }
            }
        }

        foreach (var (taskName, taskId) in taskIdsByName)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE WorkSession
                SET TaskId = @taskId
                WHERE TaskName = @taskName AND TaskId IS NULL;
                """;
            update.Parameters.AddWithValue("@taskId", taskId);
            update.Parameters.AddWithValue("@taskName", taskName);
            update.ExecuteNonQuery();
        }

        if (taskIdsByName.Count > 0)
        {
            SetAppSetting(connection, transaction, "UpgradedFromV1", "true");
        }
    }

    private static void SetAppSetting(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO AppSetting (Key, Value) VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    private static string? GetExistingTaskId(SqliteConnection connection, SqliteTransaction transaction, string title)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM \"Task\" WHERE Title = @title LIMIT 1;";
        command.Parameters.AddWithValue("@title", title);
        var result = command.ExecuteScalar();
        return result is null || result is DBNull ? null : (string)result;
    }
}
