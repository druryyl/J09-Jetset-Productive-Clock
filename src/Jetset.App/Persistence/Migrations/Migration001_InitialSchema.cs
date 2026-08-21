using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration001_InitialSchema : IMigration
{
    public int Version => 1;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS WorkSession (
                Id TEXT PRIMARY KEY NOT NULL,
                TaskName TEXT NOT NULL,
                Mode INTEGER NOT NULL,
                StartedAt TEXT NOT NULL,
                FinishedAt TEXT NULL,
                CountdownDurationTicks INTEGER NULL,
                State INTEGER NOT NULL,
                Note TEXT NULL,
                LastHeartbeatAt TEXT NULL,
                CountdownEndsAt TEXT NULL,
                CountdownRemainingTicks INTEGER NULL,
                CountdownCompletedNotified INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS WorkInterval (
                Id TEXT PRIMARY KEY NOT NULL,
                WorkSessionId TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT NULL,
                FOREIGN KEY (WorkSessionId) REFERENCES WorkSession(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_WorkInterval_Session
                ON WorkInterval(WorkSessionId);

            CREATE TABLE IF NOT EXISTS AppSetting (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
