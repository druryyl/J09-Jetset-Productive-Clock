// DOMAIN-REALIGNMENT: Historical migration only — ContextSnapshot table dropped in planned Migration011 (R-16). Do not extend.

using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration005_AddContextSnapshotTable : IMigration
{
    public int Version => 5;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS ContextSnapshot (
                Id TEXT PRIMARY KEY NOT NULL,
                TaskId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CurrentStatus TEXT NULL,
                LastProgress TEXT NULL,
                NextAction TEXT NULL,
                Blocker TEXT NULL,
                Notes TEXT NULL,
                FOREIGN KEY (TaskId) REFERENCES "Task"(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_ContextSnapshot_TaskId_CreatedAt
                ON ContextSnapshot(TaskId, CreatedAt DESC);
            """;
        command.ExecuteNonQuery();
    }
}
