using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class ProjectStore : IProjectStore
{
    private readonly SqliteConnectionFactory _factory;

    public ProjectStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public Project? Get(Guid id)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Name, Deadline, ContextText, ContextUpdatedAt, CreatedAt, UpdatedAt
            FROM Project
            WHERE Id = @id;
            """;
        command.Parameters.AddWithValue("@id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadProject(reader) : null;
    }

    public IReadOnlyList<Project> List()
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Name, Deadline, ContextText, ContextUpdatedAt, CreatedAt, UpdatedAt
            FROM Project
            ORDER BY UpdatedAt DESC;
            """;

        var results = new List<Project>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadProject(reader));
        }

        return results;
    }

    public IReadOnlyList<ProjectSummary> ListWithTaskCounts()
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT p.Id, p.Name, p.Deadline, p.ContextText, p.ContextUpdatedAt, p.CreatedAt, p.UpdatedAt,
                   COUNT(t.Id) AS TaskCount
            FROM Project p
            LEFT JOIN "Task" t ON t.ProjectId = p.Id
            GROUP BY p.Id, p.Name, p.Deadline, p.ContextText, p.ContextUpdatedAt, p.CreatedAt, p.UpdatedAt
            ORDER BY p.UpdatedAt DESC;
            """;

        var results = new List<ProjectSummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ProjectSummary
            {
                Project = ReadProject(reader),
                TaskCount = reader.GetInt32(7)
            });
        }

        return results;
    }

    public void Insert(Project project)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Project (Id, Name, Deadline, ContextText, ContextUpdatedAt, CreatedAt, UpdatedAt)
            VALUES (@id, @name, @deadline, @contextText, @contextUpdatedAt, @createdAt, @updatedAt);
            """;
        BindProject(command, project);
        command.ExecuteNonQuery();
    }

    public void Update(Project project)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Project SET
                Name = @name,
                Deadline = @deadline,
                ContextText = @contextText,
                ContextUpdatedAt = @contextUpdatedAt,
                UpdatedAt = @updatedAt
            WHERE Id = @id;
            """;
        BindProject(command, project, includeCreatedAt: false);
        command.ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Project WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());
        command.ExecuteNonQuery();
    }

    private static void BindProject(SqliteCommand command, Project project, bool includeCreatedAt = true)
    {
        command.Parameters.AddWithValue("@id", project.Id.ToString());
        command.Parameters.AddWithValue("@name", project.Name);
        command.Parameters.AddWithValue(
            "@deadline",
            (object?)project.Deadline?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("@contextText", (object?)project.ContextText ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@contextUpdatedAt",
            (object?)project.ContextUpdatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        if (includeCreatedAt)
        {
            command.Parameters.AddWithValue(
                "@createdAt",
                project.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        command.Parameters.AddWithValue(
            "@updatedAt",
            project.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    private static Project ReadProject(SqliteDataReader reader)
    {
        return new Project
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            Deadline = reader.IsDBNull(2)
                ? null
                : DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ContextText = reader.IsDBNull(3) ? null : reader.GetString(3),
            ContextUpdatedAt = reader.IsDBNull(4)
                ? null
                : DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture)
        };
    }
}
