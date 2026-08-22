using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration012_AddTaskEstimateMinutes : IMigration
{
    public int Version => 12;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """ALTER TABLE "Task" ADD COLUMN EstimateMinutes INTEGER NULL;""";
        command.ExecuteNonQuery();
    }
}
