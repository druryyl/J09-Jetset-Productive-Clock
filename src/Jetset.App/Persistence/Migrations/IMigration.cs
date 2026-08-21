using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public interface IMigration
{
    int Version { get; }
    void Up(SqliteConnection connection, SqliteTransaction transaction);
}
