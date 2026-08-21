using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration004_AddMilestoneTable : IMigration
{
    public int Version => 4;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Milestone (
                Id TEXT PRIMARY KEY NOT NULL,
                ProjectId TEXT NOT NULL,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Milestone_ProjectId_SortOrder
                ON Milestone(ProjectId, SortOrder);

            CREATE INDEX IF NOT EXISTS IX_Task_MilestoneId ON "Task"(MilestoneId);
            """;
        command.ExecuteNonQuery();
    }
}
