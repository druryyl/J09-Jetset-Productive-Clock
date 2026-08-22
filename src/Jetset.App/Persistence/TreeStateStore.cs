using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class TreeStateStore : ITreeStateStore
{
    private const string SettingKey = "WorkTreeExpandedProjects";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly SqliteConnectionFactory _factory;
    private HashSet<Guid>? _expanded;

    public TreeStateStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public IReadOnlySet<Guid> GetExpandedProjectIds() => Expanded;

    public bool IsExpanded(Guid projectId) => Expanded.Contains(projectId);

    public void SetExpanded(Guid projectId, bool expanded)
    {
        if (expanded)
            Expanded.Add(projectId);
        else
            Expanded.Remove(projectId);

        Persist(Expanded);
    }

    private HashSet<Guid> Expanded => _expanded ??= Load();

    private HashSet<Guid> Load()
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSetting WHERE Key = @key;";
        command.Parameters.AddWithValue("@key", SettingKey);

        var value = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            var ids = JsonSerializer.Deserialize<List<Guid>>(value, JsonOptions);
            return ids is null ? [] : [.. ids];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Persist(HashSet<Guid> expanded)
    {
        var json = JsonSerializer.Serialize(expanded.OrderBy(id => id).ToList(), JsonOptions);

        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO AppSetting (Key, Value) VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("@key", SettingKey);
        command.Parameters.AddWithValue("@value", json);
        command.ExecuteNonQuery();
    }
}
