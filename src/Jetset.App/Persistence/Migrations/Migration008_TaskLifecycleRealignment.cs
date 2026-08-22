using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration008_TaskLifecycleRealignment : IMigration
{
    private const int LegacyActive = 0;
    private const int LegacyBlocked = 1;
    private const int LegacyDone = 2;
    private const int LegacyCancelled = 3;

    public int Version => 8;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        AddColumns(connection, transaction);
        RemapStatuses(connection, transaction);
        SetCompletedAtForDoneTasks(connection, transaction);
    }

    private static void AddColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var origin = connection.CreateCommand();
        origin.Transaction = transaction;
        origin.CommandText = """ALTER TABLE "Task" ADD COLUMN Origin INTEGER NOT NULL DEFAULT 0;""";
        origin.ExecuteNonQuery();

        using var completedAt = connection.CreateCommand();
        completedAt.Transaction = transaction;
        completedAt.CommandText = """ALTER TABLE "Task" ADD COLUMN CompletedAt TEXT NULL;""";
        completedAt.ExecuteNonQuery();
    }

    private static void RemapStatuses(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var addColumn = connection.CreateCommand();
        addColumn.Transaction = transaction;
        addColumn.CommandText = """ALTER TABLE "Task" ADD COLUMN StatusNew INTEGER;""";
        addColumn.ExecuteNonQuery();

        using (var mapDefaults = connection.CreateCommand())
        {
            mapDefaults.Transaction = transaction;
            mapDefaults.CommandText =
                """
                UPDATE "Task"
                SET StatusNew = CASE Status
                    WHEN @legacyActive THEN @ready
                    WHEN @legacyBlocked THEN @waiting
                    WHEN @legacyDone THEN @done
                    WHEN @legacyCancelled THEN @cancelled
                    ELSE @ready
                END;
                """;
            mapDefaults.Parameters.AddWithValue("@legacyActive", LegacyActive);
            mapDefaults.Parameters.AddWithValue("@legacyBlocked", LegacyBlocked);
            mapDefaults.Parameters.AddWithValue("@legacyDone", LegacyDone);
            mapDefaults.Parameters.AddWithValue("@legacyCancelled", LegacyCancelled);
            mapDefaults.Parameters.AddWithValue("@ready", (int)Models.TaskStatus.Ready);
            mapDefaults.Parameters.AddWithValue("@waiting", (int)Models.TaskStatus.Waiting);
            mapDefaults.Parameters.AddWithValue("@done", (int)Models.TaskStatus.Done);
            mapDefaults.Parameters.AddWithValue("@cancelled", (int)Models.TaskStatus.Cancelled);
            mapDefaults.ExecuteNonQuery();
        }

        if (WorkSessionHasTaskIdColumn(connection, transaction))
        {
            using (var markInProgress = connection.CreateCommand())
            {
                markInProgress.Transaction = transaction;
                markInProgress.CommandText =
                    """
                    UPDATE "Task"
                    SET StatusNew = @running
                    WHERE Status = @legacyActive
                      AND Id IN (
                          SELECT TaskId
                          FROM WorkSession
                          WHERE TaskId IS NOT NULL
                            AND State IN (@sessionRunning, @sessionPaused)
                      );
                    """;
                markInProgress.Parameters.AddWithValue("@running", (int)Models.TaskStatus.Running);
                markInProgress.Parameters.AddWithValue("@legacyActive", LegacyActive);
                markInProgress.Parameters.AddWithValue("@sessionRunning", (int)SessionState.Running);
                markInProgress.Parameters.AddWithValue("@sessionPaused", (int)SessionState.Paused);
                markInProgress.ExecuteNonQuery();
            }

            var newestRunningTaskId = GetNewestInProgressTaskId(connection, transaction);
            if (newestRunningTaskId is not null)
            {
                using var keepNewest = connection.CreateCommand();
                keepNewest.Transaction = transaction;
                keepNewest.CommandText =
                    """
                    UPDATE "Task"
                    SET StatusNew = @running
                    WHERE Id = @taskId;
                    """;
                keepNewest.Parameters.AddWithValue("@running", (int)Models.TaskStatus.Running);
                keepNewest.Parameters.AddWithValue("@taskId", newestRunningTaskId);
                keepNewest.ExecuteNonQuery();

                using var demoteOthers = connection.CreateCommand();
                demoteOthers.Transaction = transaction;
                demoteOthers.CommandText =
                    """
                    UPDATE "Task"
                    SET StatusNew = @ready
                    WHERE StatusNew = @running
                      AND Id != @taskId;
                    """;
                demoteOthers.Parameters.AddWithValue("@ready", (int)Models.TaskStatus.Ready);
                demoteOthers.Parameters.AddWithValue("@running", (int)Models.TaskStatus.Running);
                demoteOthers.Parameters.AddWithValue("@taskId", newestRunningTaskId);
                demoteOthers.ExecuteNonQuery();
            }
        }

        using var apply = connection.CreateCommand();
        apply.Transaction = transaction;
        apply.CommandText = """UPDATE "Task" SET Status = StatusNew;""";
        apply.ExecuteNonQuery();

        using var dropColumn = connection.CreateCommand();
        dropColumn.Transaction = transaction;
        dropColumn.CommandText = """ALTER TABLE "Task" DROP COLUMN StatusNew;""";
        dropColumn.ExecuteNonQuery();
    }

    private static bool WorkSessionHasTaskIdColumn(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM pragma_table_info('WorkSession')
            WHERE name = 'TaskId';
            """;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static string? GetNewestInProgressTaskId(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT TaskId
            FROM WorkSession
            WHERE TaskId IS NOT NULL
              AND State IN (@sessionRunning, @sessionPaused)
            ORDER BY StartedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@sessionRunning", (int)SessionState.Running);
        command.Parameters.AddWithValue("@sessionPaused", (int)SessionState.Paused);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : (string)result;
    }

    private static void SetCompletedAtForDoneTasks(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE "Task"
            SET CompletedAt = UpdatedAt
            WHERE Status = @done
              AND CompletedAt IS NULL;
            """;
        command.Parameters.AddWithValue("@done", (int)Models.TaskStatus.Done);
        command.ExecuteNonQuery();
    }
}
