using Microsoft.Data.Sqlite;
using System.IO;

namespace Jetset.App.Persistence;

public sealed class SqliteConnectionFactory
{
    public SqliteConnectionFactory(string databasePath)
    {
        DatabasePath = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    }

    public string DatabasePath { get; }

    public static SqliteConnectionFactory CreateDefault()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Jetset");
        return new SqliteConnectionFactory(Path.Combine(folder, "jetset.db"));
    }

    public SqliteConnection Create()
    {
        var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();
        return connection;
    }
}
