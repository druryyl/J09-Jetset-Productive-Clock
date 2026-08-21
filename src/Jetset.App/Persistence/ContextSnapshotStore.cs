using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class ContextSnapshotStore : IContextSnapshotStore
{
    private readonly SqliteConnectionFactory _factory;

    public ContextSnapshotStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Insert(ContextSnapshot snapshot)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ContextSnapshot (
                Id, TaskId, CreatedAt, CurrentStatus, LastProgress, NextAction, Blocker, Notes
            )
            VALUES (
                @id, @taskId, @createdAt, @currentStatus, @lastProgress, @nextAction, @blocker, @notes
            );
            """;
        BindSnapshot(command, snapshot);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<ContextSnapshot> ListByTask(Guid taskId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, CreatedAt, CurrentStatus, LastProgress, NextAction, Blocker, Notes
            FROM ContextSnapshot
            WHERE TaskId = @taskId
            ORDER BY CreatedAt DESC;
            """;
        command.Parameters.AddWithValue("@taskId", taskId.ToString());

        var results = new List<ContextSnapshot>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadSnapshot(reader));
        }

        return results;
    }

    public ContextSnapshot? GetLatest(Guid taskId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, CreatedAt, CurrentStatus, LastProgress, NextAction, Blocker, Notes
            FROM ContextSnapshot
            WHERE TaskId = @taskId
            ORDER BY CreatedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@taskId", taskId.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSnapshot(reader) : null;
    }

    public void DeleteByTask(Guid taskId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ContextSnapshot WHERE TaskId = @taskId;";
        command.Parameters.AddWithValue("@taskId", taskId.ToString());
        command.ExecuteNonQuery();
    }

    private static void BindSnapshot(SqliteCommand command, ContextSnapshot snapshot)
    {
        command.Parameters.AddWithValue("@id", snapshot.Id.ToString());
        command.Parameters.AddWithValue("@taskId", snapshot.TaskId.ToString());
        command.Parameters.AddWithValue(
            "@createdAt",
            snapshot.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@currentStatus", (object?)snapshot.CurrentStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastProgress", (object?)snapshot.LastProgress ?? DBNull.Value);
        command.Parameters.AddWithValue("@nextAction", (object?)snapshot.NextAction ?? DBNull.Value);
        command.Parameters.AddWithValue("@blocker", (object?)snapshot.Blocker ?? DBNull.Value);
        command.Parameters.AddWithValue("@notes", (object?)snapshot.Notes ?? DBNull.Value);
    }

    private static ContextSnapshot ReadSnapshot(SqliteDataReader reader)
    {
        return new ContextSnapshot
        {
            Id = Guid.Parse(reader.GetString(0)),
            TaskId = Guid.Parse(reader.GetString(1)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            CurrentStatus = reader.IsDBNull(3) ? null : reader.GetString(3),
            LastProgress = reader.IsDBNull(4) ? null : reader.GetString(4),
            NextAction = reader.IsDBNull(5) ? null : reader.GetString(5),
            Blocker = reader.IsDBNull(6) ? null : reader.GetString(6),
            Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
