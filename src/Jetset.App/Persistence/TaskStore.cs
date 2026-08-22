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

            SELECT Id, Title, Status, Origin, Notes, EstimateMinutes, ProjectId, CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt

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

            SELECT Id, Title, Status, Origin, Notes, EstimateMinutes, ProjectId, CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt

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



    public IReadOnlyList<WorkTask> ListByProject(Guid? projectId)

    {

        using var connection = _factory.Create();

        using var command = connection.CreateCommand();

        if (projectId is null)

        {

            command.CommandText =

                """

                SELECT Id, Title, Status, Origin, Notes, EstimateMinutes, ProjectId, CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt

                FROM "Task"

                WHERE ProjectId IS NULL

                ORDER BY UpdatedAt DESC;

                """;

        }

        else

        {

            command.CommandText =

                """

                SELECT Id, Title, Status, Origin, Notes, EstimateMinutes, ProjectId, CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt

                FROM "Task"

                WHERE ProjectId = @projectId

                ORDER BY UpdatedAt DESC;

                """;

            command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());

        }



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

            SELECT t.Id, t.Title, t.Status, t.Origin, t.Notes, t.EstimateMinutes, t.ProjectId, t.CreatedAt, t.CompletedAt, t.UpdatedAt, t.LastWorkedAt

            FROM "Task" t

            LEFT JOIN Project p ON t.ProjectId = p.Id

            WHERE t.Title LIKE @query COLLATE NOCASE

               OR t.Notes LIKE @query COLLATE NOCASE

               OR p.ContextText LIKE @query COLLATE NOCASE

            ORDER BY t.UpdatedAt DESC;

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



    public IReadOnlyList<WorkTask> ListByStatuses(IReadOnlyList<Models.TaskStatus> statuses)

    {

        ArgumentNullException.ThrowIfNull(statuses);

        if (statuses.Count == 0)

        {

            return [];

        }



        using var connection = _factory.Create();

        using var command = connection.CreateCommand();



        var placeholders = new string[statuses.Count];

        for (var i = 0; i < statuses.Count; i++)

        {

            var name = "@s" + i;

            placeholders[i] = name;

            command.Parameters.AddWithValue(name, (int)statuses[i]);

        }



        command.CommandText =

            $"""

            SELECT Id, Title, Status, Origin, Notes, EstimateMinutes, ProjectId, CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt

            FROM "Task"

            WHERE Status IN ({string.Join(", ", placeholders)})

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



    public int CountByProject(Guid projectId)

    {

        using var connection = _factory.Create();

        using var command = connection.CreateCommand();

        command.CommandText = """SELECT COUNT(*) FROM "Task" WHERE ProjectId = @projectId;""";

        command.Parameters.AddWithValue("@projectId", projectId.ToString());

        return Convert.ToInt32(command.ExecuteScalar());

    }



    public void UnassignAllFromProject(Guid projectId)

    {

        using var connection = _factory.Create();

        using var command = connection.CreateCommand();

        command.CommandText =

            """

            UPDATE "Task" SET

                ProjectId = NULL,

                UpdatedAt = @updatedAt

            WHERE ProjectId = @projectId;

            """;

        command.Parameters.AddWithValue("@projectId", projectId.ToString());

        command.Parameters.AddWithValue(

            "@updatedAt",

            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        command.ExecuteNonQuery();

    }



    public void Insert(WorkTask task)

    {

        using var connection = _factory.Create();

        using var command = connection.CreateCommand();

        command.CommandText =

            """

            INSERT INTO "Task" (

                Id, Title, Status, Origin, Notes, EstimateMinutes,

                ProjectId, CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt)

            VALUES (

                @id, @title, @status, @origin, @notes, @estimateMinutes,

                @projectId, @createdAt, @completedAt, @updatedAt, @lastWorkedAt);

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

                Origin = @origin,

                Notes = @notes,

                EstimateMinutes = @estimateMinutes,

                ProjectId = @projectId,

                CompletedAt = @completedAt,

                UpdatedAt = @updatedAt,

                LastWorkedAt = @lastWorkedAt

            WHERE Id = @id;

            """;

        BindTask(command, task, includeCreatedAt: false);

        command.ExecuteNonQuery();

    }



    public WorkTask? GetRunningTask()

    {

        using var connection = _factory.Create();

        using var command = connection.CreateCommand();

        command.CommandText =

            """

            SELECT Id, Title, Status, Origin, Notes, EstimateMinutes, ProjectId, CreatedAt, CompletedAt, UpdatedAt, LastWorkedAt

            FROM "Task"

            WHERE Status = @running

            LIMIT 2;

            """;

        command.Parameters.AddWithValue("@running", (int)Models.TaskStatus.Running);



        using var reader = command.ExecuteReader();

        WorkTask? running = null;

        while (reader.Read())

        {

            if (running is not null)

            {

                throw new InvalidOperationException("Multiple Running tasks found.");

            }



            running = ReadTask(reader);

        }



        return running;

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

        command.Parameters.AddWithValue("@origin", (int)task.Origin);

        command.Parameters.AddWithValue("@notes", (object?)task.Notes ?? DBNull.Value);

        command.Parameters.AddWithValue("@estimateMinutes", (object?)task.EstimateMinutes ?? DBNull.Value);

        command.Parameters.AddWithValue("@projectId", (object?)task.ProjectId?.ToString() ?? DBNull.Value);

        if (includeCreatedAt)

        {

            command.Parameters.AddWithValue(

                "@createdAt",

                task.CreatedAt.ToString("O", CultureInfo.InvariantCulture));

        }



        command.Parameters.AddWithValue(

            "@completedAt",

            (object?)task.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);

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

            Origin = (TaskOrigin)reader.GetInt32(3),

            Notes = reader.IsDBNull(4) ? null : reader.GetString(4),

            EstimateMinutes = reader.IsDBNull(5) ? null : reader.GetInt32(5),

            ProjectId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),

            CreatedAt = DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),

            CompletedAt = reader.IsDBNull(8)

                ? null

                : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture),

            UpdatedAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),

            LastWorkedAt = reader.IsDBNull(10)

                ? null

                : DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture)

        };

    }

}

