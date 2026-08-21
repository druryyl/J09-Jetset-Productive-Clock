using Jetset.App.Persistence.Migrations;

namespace Jetset.App.Persistence;

public sealed class SchemaInitializer
{
    private static readonly IMigration[] Migrations =
    [
        new Migration001_InitialSchema(),
        new Migration002_AddTaskTable(),
        new Migration003_AddProjectTable(),
        new Migration004_AddMilestoneTable(),
        new Migration005_AddContextSnapshotTable(),
        new Migration006_AddWorkSessionTaskId(),
        new Migration007_AddTaskSwitchEventTable()
    ];

    private readonly SqliteConnectionFactory _factory;

    public SchemaInitializer(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Initialize()
    {
        var runner = new MigrationRunner(_factory, Migrations);
        var currentVersion = runner.GetCurrentVersion();
        var targetVersion = Migrations.Max(m => m.Version);

        new DatabaseBackupService().CreatePreMigrationBackup(_factory, currentVersion, targetVersion);
        runner.RunPending();

        var validation = new MigrationValidationService().Validate(_factory);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Database migration validation failed: " + string.Join(" ", validation.Errors));
        }
    }
}
