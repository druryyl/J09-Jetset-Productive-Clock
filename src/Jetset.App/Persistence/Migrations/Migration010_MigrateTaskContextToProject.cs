using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration010_MigrateTaskContextToProject : IMigration
{
    public int Version => 10;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        MigrateTaskContextToProjects(connection, transaction);
        DropTaskContextColumns(connection, transaction);
    }

    private static void MigrateTaskContextToProjects(SqliteConnection connection, SqliteTransaction transaction)
    {
        if (!TaskHasContextColumns(connection, transaction))
        {
            return;
        }

        var projectIds = GetProjectIds(connection, transaction);
        foreach (var projectId in projectIds)
        {
            var taskContext = GetMostRecentTaskContext(connection, transaction, projectId);
            if (taskContext is null)
            {
                continue;
            }

            var contextText = BuildContextText(
                taskContext.Value.CurrentStatus,
                taskContext.Value.LastProgress,
                taskContext.Value.NextAction,
                taskContext.Value.Blocker);

            if (contextText is null)
            {
                continue;
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE Project
                SET ContextText = @contextText,
                    ContextUpdatedAt = @contextUpdatedAt
                WHERE Id = @projectId
                  AND (ContextText IS NULL OR ContextText = '');
                """;
            update.Parameters.AddWithValue("@contextText", contextText);
            update.Parameters.AddWithValue(
                "@contextUpdatedAt",
                taskContext.Value.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("@projectId", projectId);
            update.ExecuteNonQuery();
        }
    }

    private static bool TaskHasContextColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM pragma_table_info('Task')
            WHERE name = 'CurrentStatus';
            """;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static List<string> GetProjectIds(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM Project;";

        var ids = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static (string? CurrentStatus, string? LastProgress, string? NextAction, string? Blocker, DateTimeOffset UpdatedAt)?
        GetMostRecentTaskContext(SqliteConnection connection, SqliteTransaction transaction, string projectId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT CurrentStatus, LastProgress, NextAction, Blocker, UpdatedAt
            FROM "Task"
            WHERE ProjectId = @projectId
            ORDER BY UpdatedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@projectId", projectId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var updatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture);
        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            updatedAt);
    }

    private static string? BuildContextText(
        string? currentStatus,
        string? lastProgress,
        string? nextAction,
        string? blocker)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(currentStatus))
        {
            parts.Add($"Current: {currentStatus.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(lastProgress))
        {
            parts.Add($"Progress: {lastProgress.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(nextAction))
        {
            parts.Add($"Next: {nextAction.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(blocker))
        {
            parts.Add($"Blocker: {blocker.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static void DropTaskContextColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        foreach (var column in new[] { "CurrentStatus", "LastProgress", "NextAction", "Blocker" })
        {
            if (!ColumnExists(connection, transaction, "Task", column))
            {
                continue;
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""ALTER TABLE "Task" DROP COLUMN {column};""";
            command.ExecuteNonQuery();
        }
    }

    private static bool ColumnExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string column)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
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
}
