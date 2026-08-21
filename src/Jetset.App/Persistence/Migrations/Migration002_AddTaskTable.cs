using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration002_AddTaskTable : IMigration
{
    public int Version => 2;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS "Task" (
                Id TEXT PRIMARY KEY NOT NULL,
                Title TEXT NOT NULL,
                Status INTEGER NOT NULL,
                Notes TEXT NULL,
                CurrentStatus TEXT NULL,
                LastProgress TEXT NULL,
                NextAction TEXT NULL,
                Blocker TEXT NULL,
                ProjectId TEXT NULL,
                MilestoneId TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastWorkedAt TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Task_Title ON "Task"(Title);
            CREATE INDEX IF NOT EXISTS IX_Task_UpdatedAt ON "Task"(UpdatedAt DESC);
            """;
        command.ExecuteNonQuery();
    }
}
