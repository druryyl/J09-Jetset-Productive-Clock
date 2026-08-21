using System.IO;

namespace Jetset.App.Persistence;

public sealed class DatabaseBackupService
{
    public string? CreatePreMigrationBackup(SqliteConnectionFactory factory, int currentVersion, int targetVersion)
    {
        if (currentVersion >= targetVersion)
        {
            return null;
        }

        if (!File.Exists(factory.DatabasePath))
        {
            return null;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = $"{factory.DatabasePath}.backup-{timestamp}";
        File.Copy(factory.DatabasePath, backupPath, overwrite: false);
        return backupPath;
    }
}
