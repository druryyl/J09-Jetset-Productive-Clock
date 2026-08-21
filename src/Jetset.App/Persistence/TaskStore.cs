using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class TaskStore : ITaskStore
{
    private readonly SqliteConnectionFactory _factory;

    public TaskStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public WorkTask? Get(Guid id)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, Status, Notes, CurrentStatus, LastProgress, NextAction, Blocker,
                   ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt
            FROM "Task"
            WHERE Id = @id;
            """;
        command.Parameters.AddWithValue("@id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTask(reader) : null;
    }

    public IReadOnlyList<WorkTask> List()
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, Status, Notes, CurrentStatus, LastProgress, NextAction, Blocker,
                   ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt
            FROM "Task"
            ORDER BY UpdatedAt DESC;
            """;

        var results = new List<WorkTask>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadTask(reader));
        }

        return results;
    }

    public IReadOnlyList<WorkTask> Search(string query)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, Status, Notes, CurrentStatus, LastProgress, NextAction, Blocker,
                   ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt
            FROM "Task"
            WHERE Title LIKE @query COLLATE NOCASE
            ORDER BY UpdatedAt DESC;
            """;
        command.Parameters.AddWithValue("@query", "%" + query + "%");

        var results = new List<WorkTask>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadTask(reader));
        }

        return results;
    }

    public void Insert(WorkTask task)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "Task" (
                Id, Title, Status, Notes, CurrentStatus, LastProgress, NextAction, Blocker,
                ProjectId, MilestoneId, CreatedAt, UpdatedAt, LastWorkedAt)
            VALUES (
                @id, @title, @status, @notes, @currentStatus, @lastProgress, @nextAction, @blocker,
                @projectId, @milestoneId, @createdAt, @updatedAt, @lastWorkedAt);
            """;
        BindTask(command, task);
        command.ExecuteNonQuery();
    }

    public void Update(WorkTask task)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE "Task" SET
                Title = @title,
                Status = @status,
                Notes = @notes,
                CurrentStatus = @currentStatus,
                LastProgress = @lastProgress,
                NextAction = @nextAction,
                Blocker = @blocker,
                ProjectId = @projectId,
                MilestoneId = @milestoneId,
                UpdatedAt = @updatedAt,
                LastWorkedAt = @lastWorkedAt
            WHERE Id = @id;
            """;
        BindTask(command, task, includeCreatedAt: false);
        command.ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = """DELETE FROM "Task" WHERE Id = @id;""";
        command.Parameters.AddWithValue("@id", id.ToString());
        command.ExecuteNonQuery();
    }

    private static void BindTask(SqliteCommand command, WorkTask task, bool includeCreatedAt = true)
    {
        command.Parameters.AddWithValue("@id", task.Id.ToString());
        command.Parameters.AddWithValue("@title", task.Title);
        command.Parameters.AddWithValue("@status", (int)task.Status);
        command.Parameters.AddWithValue("@notes", (object?)task.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("@currentStatus", (object?)task.CurrentStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastProgress", (object?)task.LastProgress ?? DBNull.Value);
        command.Parameters.AddWithValue("@nextAction", (object?)task.NextAction ?? DBNull.Value);
        command.Parameters.AddWithValue("@blocker", (object?)task.Blocker ?? DBNull.Value);
        command.Parameters.AddWithValue("@projectId", (object?)task.ProjectId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("@milestoneId", (object?)task.MilestoneId?.ToString() ?? DBNull.Value);
        if (includeCreatedAt)
        {
            command.Parameters.AddWithValue(
                "@createdAt",
                task.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        command.Parameters.AddWithValue(
            "@updatedAt",
            task.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "@lastWorkedAt",
            (object?)task.LastWorkedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
    }

    private static WorkTask ReadTask(SqliteDataReader reader)
    {
        return new WorkTask
        {
            Id = Guid.Parse(reader.GetString(0)),
            Title = reader.GetString(1),
            Status = (Models.TaskStatus)reader.GetInt32(2),
            Notes = reader.IsDBNull(3) ? null : reader.GetString(3),
            CurrentStatus = reader.IsDBNull(4) ? null : reader.GetString(4),
            LastProgress = reader.IsDBNull(5) ? null : reader.GetString(5),
            NextAction = reader.IsDBNull(6) ? null : reader.GetString(6),
            Blocker = reader.IsDBNull(7) ? null : reader.GetString(7),
            ProjectId = reader.IsDBNull(8) ? null : Guid.Parse(reader.GetString(8)),
            MilestoneId = reader.IsDBNull(9) ? null : Guid.Parse(reader.GetString(9)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(11), CultureInfo.InvariantCulture),
            LastWorkedAt = reader.IsDBNull(12)
                ? null
                : DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture)
        };
    }
}
