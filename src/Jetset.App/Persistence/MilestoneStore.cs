using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class MilestoneStore : IMilestoneStore
{
    private readonly SqliteConnectionFactory _factory;

    public MilestoneStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public Milestone? Get(Guid id)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ProjectId, Name, SortOrder, CreatedAt
            FROM Milestone
            WHERE Id = @id;
            """;
        command.Parameters.AddWithValue("@id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadMilestone(reader) : null;
    }

    public IReadOnlyList<Milestone> ListByProject(Guid projectId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ProjectId, Name, SortOrder, CreatedAt
            FROM Milestone
            WHERE ProjectId = @projectId
            ORDER BY SortOrder ASC;
            """;
        command.Parameters.AddWithValue("@projectId", projectId.ToString());

        var results = new List<Milestone>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadMilestone(reader));
        }

        return results;
    }

    public void Insert(Milestone milestone)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Milestone (Id, ProjectId, Name, SortOrder, CreatedAt)
            VALUES (@id, @projectId, @name, @sortOrder, @createdAt);
            """;
        BindMilestone(command, milestone);
        command.ExecuteNonQuery();
    }

    public void Update(Milestone milestone)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Milestone SET
                Name = @name,
                SortOrder = @sortOrder
            WHERE Id = @id;
            """;
        command.Parameters.AddWithValue("@id", milestone.Id.ToString());
        command.Parameters.AddWithValue("@name", milestone.Name);
        command.Parameters.AddWithValue("@sortOrder", milestone.SortOrder);
        command.ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Milestone WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());
        command.ExecuteNonQuery();
    }

    public void DeleteByProject(Guid projectId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Milestone WHERE ProjectId = @projectId;";
        command.Parameters.AddWithValue("@projectId", projectId.ToString());
        command.ExecuteNonQuery();
    }

    public void UpdateSortOrders(Guid projectId, IReadOnlyList<Guid> orderedIds)
    {
        using var connection = _factory.Create();
        using var transaction = connection.BeginTransaction();
        try
        {
            for (var i = 0; i < orderedIds.Count; i++)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    UPDATE Milestone SET SortOrder = @sortOrder
                    WHERE Id = @id AND ProjectId = @projectId;
                    """;
                command.Parameters.AddWithValue("@sortOrder", i);
                command.Parameters.AddWithValue("@id", orderedIds[i].ToString());
                command.Parameters.AddWithValue("@projectId", projectId.ToString());
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void BindMilestone(SqliteCommand command, Milestone milestone)
    {
        command.Parameters.AddWithValue("@id", milestone.Id.ToString());
        command.Parameters.AddWithValue("@projectId", milestone.ProjectId.ToString());
        command.Parameters.AddWithValue("@name", milestone.Name);
        command.Parameters.AddWithValue("@sortOrder", milestone.SortOrder);
        command.Parameters.AddWithValue(
            "@createdAt",
            milestone.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    private static Milestone ReadMilestone(SqliteDataReader reader)
    {
        return new Milestone
        {
            Id = Guid.Parse(reader.GetString(0)),
            ProjectId = Guid.Parse(reader.GetString(1)),
            Name = reader.GetString(2),
            SortOrder = reader.GetInt32(3),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)
        };
    }
}
