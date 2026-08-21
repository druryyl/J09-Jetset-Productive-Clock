using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence.Migrations;

public sealed class MigrationRunner
{
    private readonly SqliteConnectionFactory _factory;
    private readonly IReadOnlyList<IMigration> _migrations;

    public MigrationRunner(SqliteConnectionFactory factory, IReadOnlyList<IMigration> migrations)
    {
        _factory = factory;
        _migrations = migrations.OrderBy(m => m.Version).ToList();
    }

    public int GetCurrentVersion()
    {
        using var connection = _factory.Create();
        EnsureSchemaVersionTable(connection);
        return GetCurrentVersion(connection);
    }

    public void RunPending()
    {
        using var connection = _factory.Create();

        EnsureSchemaVersionTable(connection);

        var currentVersion = GetCurrentVersion(connection);
        foreach (var migration in _migrations.Where(m => m.Version > currentVersion))
        {
            using var tx = connection.BeginTransaction();
            migration.Up(connection, tx);

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText =
                    """
                    INSERT INTO SchemaVersion (Version, AppliedAt)
                    VALUES (@version, @appliedAt);
                    """;
                insert.Parameters.AddWithValue("@version", migration.Version);
                insert.Parameters.AddWithValue(
                    "@appliedAt",
                    DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                insert.ExecuteNonQuery();
            }

            tx.Commit();
        }

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
    }

    private static void EnsureSchemaVersionTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                Version INTEGER PRIMARY KEY NOT NULL,
                AppliedAt TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static int GetCurrentVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }
}
