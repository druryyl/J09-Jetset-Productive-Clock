using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class Migration009_AddProjectContextText : IMigration
{
    public int Version => 9;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var contextText = connection.CreateCommand();
        contextText.Transaction = transaction;
        contextText.CommandText = """ALTER TABLE Project ADD COLUMN ContextText TEXT NULL;""";
        contextText.ExecuteNonQuery();

        using var contextUpdatedAt = connection.CreateCommand();
        contextUpdatedAt.Transaction = transaction;
        contextUpdatedAt.CommandText = """ALTER TABLE Project ADD COLUMN ContextUpdatedAt TEXT NULL;""";
        contextUpdatedAt.ExecuteNonQuery();
    }
}
