using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration011_SchemaCleanup : IMigration
{
    public int Version => 11;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        ClearMilestoneReferences(connection, transaction);
        DropTaskDeprecatedColumns(connection, transaction);
        DropTableIfExists(connection, transaction, "ContextSnapshot");
        DropTableIfExists(connection, transaction, "TaskSwitchEvent");
        DropTableIfExists(connection, transaction, "Milestone");
    }

    private static void ClearMilestoneReferences(SqliteConnection connection, SqliteTransaction transaction)
    {
        if (!ColumnExists(connection, transaction, "Task", "MilestoneId"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """UPDATE "Task" SET MilestoneId = NULL WHERE MilestoneId IS NOT NULL;""";
        command.ExecuteNonQuery();
    }

    private static void DropTaskDeprecatedColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        DropIndexIfExists(connection, transaction, "IX_Task_MilestoneId");

        foreach (var column in new[]
                 {
                     "MilestoneId",
                     "CurrentStatus",
                     "LastProgress",
                     "NextAction",
                     "Blocker"
                 })
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

    private static void DropTableIfExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName)
    {
        if (!TableExists(connection, transaction, tableName))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DROP TABLE {tableName};";
        command.ExecuteNonQuery();
    }

    private static void DropIndexIfExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string indexName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'index' AND name = @name;
            """;
        command.Parameters.AddWithValue("@name", indexName);
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            return;
        }

        using var drop = connection.CreateCommand();
        drop.Transaction = transaction;
        drop.CommandText = $"DROP INDEX {indexName};";
        drop.ExecuteNonQuery();
    }

    private static bool TableExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = @name;
            """;
        command.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
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
