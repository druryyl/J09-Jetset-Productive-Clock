// DOMAIN-REALIGNMENT: Historical migration only — TaskSwitchEvent table dropped in planned Migration011 (R-16). Do not extend.

using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration007_AddTaskSwitchEventTable : IMigration
{
    public int Version => 7;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS TaskSwitchEvent (
                Id TEXT PRIMARY KEY NOT NULL,
                FromTaskId TEXT NULL,
                ToTaskId TEXT NOT NULL,
                OccurredAt TEXT NOT NULL,
                FOREIGN KEY (FromTaskId) REFERENCES "Task"(Id) ON DELETE SET NULL,
                FOREIGN KEY (ToTaskId) REFERENCES "Task"(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_TaskSwitchEvent_OccurredAt
                ON TaskSwitchEvent(OccurredAt);
            """;
        command.ExecuteNonQuery();
    }
}
