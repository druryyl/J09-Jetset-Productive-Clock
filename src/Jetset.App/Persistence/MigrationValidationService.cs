using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class MigrationValidationResult
{
    public bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class MigrationValidationService
{
    public MigrationValidationResult Validate(SqliteConnectionFactory factory)
    {
        var errors = new List<string>();
        using var connection = factory.Create();

        if (!TableExists(connection, "WorkSession"))
        {
            return new MigrationValidationResult { IsValid = true };
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM WorkSession WHERE TaskId IS NULL;";
            var nullCount = Convert.ToInt64(command.ExecuteScalar());
            if (nullCount > 0)
            {
                errors.Add($"{nullCount} work session(s) have no linked task.");
            }
        }

        if (TableExists(connection, "Task"))
        {
            var schemaVersion = GetSchemaVersion(connection);

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT COUNT(*) FROM WorkSession s
                    WHERE s.TaskId IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM "Task" t WHERE t.Id = s.TaskId);
                    """;
                var orphanCount = Convert.ToInt64(command.ExecuteScalar());
                if (orphanCount > 0)
                {
                    errors.Add($"{orphanCount} work session(s) reference missing tasks.");
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT COUNT(*) FROM WorkSession s
                    INNER JOIN "Task" t ON t.Id = s.TaskId
                    WHERE s.TaskName != t.Title;
                    """;
                var mismatchCount = Convert.ToInt64(command.ExecuteScalar());
                if (mismatchCount > 0)
                {
                    errors.Add($"{mismatchCount} work session(s) have a task name that does not match the linked task title.");
                }
            }

            foreach (var column in new[] { "MilestoneId", "CurrentStatus", "LastProgress", "NextAction", "Blocker" })
            {
                if (ColumnExists(connection, "Task", column))
                {
                    errors.Add($"Deprecated Task column '{column}' is still present.");
                }
            }

            foreach (var column in new[] { "Origin", "CompletedAt" })
            {
                if (!ColumnExists(connection, "Task", column))
                {
                    errors.Add($"Required Task column '{column}' is missing.");
                }
            }

            if (schemaVersion >= 12 && !ColumnExists(connection, "Task", "EstimateMinutes"))
            {
                errors.Add("Required Task column 'EstimateMinutes' is missing.");
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT COUNT(*) FROM "Task"
                    WHERE Status NOT IN (@inbox, @ready, @running, @waiting, @done, @cancelled);
                    """;
                command.Parameters.AddWithValue("@inbox", (int)Models.TaskStatus.Inbox);
                command.Parameters.AddWithValue("@ready", (int)Models.TaskStatus.Ready);
                command.Parameters.AddWithValue("@running", (int)Models.TaskStatus.Running);
                command.Parameters.AddWithValue("@waiting", (int)Models.TaskStatus.Waiting);
                command.Parameters.AddWithValue("@done", (int)Models.TaskStatus.Done);
                command.Parameters.AddWithValue("@cancelled", (int)Models.TaskStatus.Cancelled);
                var invalidStatusCount = Convert.ToInt64(command.ExecuteScalar());
                if (invalidStatusCount > 0)
                {
                    errors.Add($"{invalidStatusCount} task(s) have an invalid status value.");
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT COUNT(*) FROM "Task" WHERE Status = @running;
                    """;
                command.Parameters.AddWithValue("@running", (int)Models.TaskStatus.Running);
                var runningCount = Convert.ToInt64(command.ExecuteScalar());
                if (runningCount > 1)
                {
                    errors.Add($"{runningCount} tasks are Running; at most one is allowed.");
                }
            }
        }

        if (TableExists(connection, "Project") && !ColumnExists(connection, "Project", "ContextText"))
        {
            errors.Add("Required Project column 'ContextText' is missing.");
        }

        if (TableExists(connection, "WorkInterval"))
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*) FROM WorkInterval i
                WHERE NOT EXISTS (SELECT 1 FROM WorkSession s WHERE s.Id = i.WorkSessionId);
                """;
            var orphanIntervalCount = Convert.ToInt64(command.ExecuteScalar());
            if (orphanIntervalCount > 0)
            {
                errors.Add($"{orphanIntervalCount} work interval(s) reference missing sessions.");
            }
        }

        foreach (var table in new[] { "Milestone", "ContextSnapshot", "TaskSwitchEvent" })
        {
            if (TableExists(connection, table))
            {
                errors.Add($"Deprecated table '{table}' is still present.");
            }
        }

        return new MigrationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name = @name;
            """;
        command.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        if (!TableExists(connection, "SchemaVersion"))
        {
            return 0;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
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
}
