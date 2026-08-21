using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class TaskSwitchEventStore : ITaskSwitchEventStore
{
    private readonly SqliteConnectionFactory _factory;

    public TaskSwitchEventStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Insert(TaskSwitchEvent switchEvent)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO TaskSwitchEvent (Id, FromTaskId, ToTaskId, OccurredAt)
            VALUES (@id, @fromTaskId, @toTaskId, @occurredAt);
            """;
        command.Parameters.AddWithValue("@id", switchEvent.Id.ToString());
        command.Parameters.AddWithValue(
            "@fromTaskId",
            switchEvent.FromTaskId is { } fromId ? fromId.ToString() : DBNull.Value);
        command.Parameters.AddWithValue("@toTaskId", switchEvent.ToTaskId.ToString());
        command.Parameters.AddWithValue(
            "@occurredAt",
            switchEvent.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<TaskSwitchEvent> ListBetween(DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, FromTaskId, ToTaskId, OccurredAt
            FROM TaskSwitchEvent
            WHERE OccurredAt >= @start AND OccurredAt < @end
            ORDER BY OccurredAt;
            """;
        command.Parameters.AddWithValue(
            "@start",
            startInclusive.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "@end",
            endExclusive.ToString("O", CultureInfo.InvariantCulture));

        var results = new List<TaskSwitchEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadEvent(reader));
        }

        return results;
    }

    private static TaskSwitchEvent ReadEvent(SqliteDataReader reader)
    {
        return new TaskSwitchEvent
        {
            Id = Guid.Parse(reader.GetString(0)),
            FromTaskId = reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)),
            ToTaskId = Guid.Parse(reader.GetString(2)),
            OccurredAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)
        };
    }
}
