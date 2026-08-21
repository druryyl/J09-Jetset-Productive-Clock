using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration003_AddProjectTable : IMigration
{
    public int Version => 3;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Project (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL,
                Deadline TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Project_UpdatedAt ON Project(UpdatedAt DESC);
            CREATE INDEX IF NOT EXISTS IX_Task_ProjectId ON "Task"(ProjectId);
            """;
        command.ExecuteNonQuery();
    }
}
