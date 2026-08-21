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
}
