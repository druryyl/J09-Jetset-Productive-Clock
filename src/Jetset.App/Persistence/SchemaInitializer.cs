using Jetset.App.Persistence.Migrations;

namespace Jetset.App.Persistence;

public sealed class SchemaInitializer
{
    private readonly SqliteConnectionFactory _factory;

    public SchemaInitializer(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Initialize()
    {
        new MigrationRunner(_factory, [
            new Migration001_InitialSchema(),
            new Migration002_AddTaskTable(),
            new Migration003_AddProjectTable(),
            new Migration004_AddMilestoneTable()
        ]).RunPending();
    }
}
