using Jetset.App.Helpers;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class SessionStore : ISessionStore
{
    private readonly SqliteConnectionFactory _factory;

    public SessionStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public WorkSession? GetActiveSession()
    {
        return GetInProgressSessions().FirstOrDefault();
    }

    public IReadOnlyList<WorkSession> GetInProgressSessions()
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskName, Mode, StartedAt, FinishedAt, CountdownDurationTicks,
                   State, Note, LastHeartbeatAt, CountdownEndsAt, CountdownRemainingTicks,
                   CountdownCompletedNotified
            FROM WorkSession
            WHERE State IN (@running, @paused)
            ORDER BY CASE State WHEN @running THEN 0 ELSE 1 END,
                     LastHeartbeatAt DESC,
                     StartedAt DESC;
            """;
        command.Parameters.AddWithValue("@running", (int)SessionState.Running);
        command.Parameters.AddWithValue("@paused", (int)SessionState.Paused);

        var results = new List<WorkSession>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadSession(reader));
        }

        return results;
    }

    public IReadOnlyList<WorkInterval> GetIntervals(Guid sessionId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, WorkSessionId, StartedAt, EndedAt
            FROM WorkInterval
            WHERE WorkSessionId = @sessionId
            ORDER BY StartedAt;
            """;
        command.Parameters.AddWithValue("@sessionId", sessionId.ToString());

        var results = new List<WorkInterval>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadInterval(reader));
        }

        return results;
    }

    public void SaveNewSession(WorkSession session, WorkInterval firstInterval)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        if (session.State == SessionState.Running)
        {
            using var check = connection.CreateCommand();
            check.Transaction = tx;
            check.CommandText =
                """
                SELECT COUNT(1) FROM WorkSession WHERE State = @running;
                """;
            check.Parameters.AddWithValue("@running", (int)SessionState.Running);
            var count = Convert.ToInt64(check.ExecuteScalar());
            if (count > 0)
            {
                throw new InvalidOperationException("Only one running work session is allowed.");
            }
        }

        InsertSession(connection, tx, session);
        InsertInterval(connection, tx, firstInterval);
        tx.Commit();
    }

    public void UpdateSession(WorkSession session)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE WorkSession SET
                TaskName = @taskName,
                FinishedAt = @finishedAt,
                State = @state,
                Note = @note,
                LastHeartbeatAt = @lastHeartbeatAt,
                CountdownEndsAt = @countdownEndsAt,
                CountdownRemainingTicks = @countdownRemainingTicks,
                CountdownCompletedNotified = @countdownCompletedNotified
            WHERE Id = @id;
            """;
        BindSessionUpdate(command, session);
        command.ExecuteNonQuery();
    }

    public void InsertInterval(WorkInterval interval)
    {
        using var connection = _factory.Create();
        InsertInterval(connection, null, interval);
    }

    public void CloseInterval(Guid intervalId, DateTimeOffset endedAt)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE WorkInterval
            SET EndedAt = @endedAt
            WHERE Id = @id AND EndedAt IS NULL;
            """;
        command.Parameters.AddWithValue("@endedAt", endedAt.ToString("O"));
        command.Parameters.AddWithValue("@id", intervalId.ToString());
        command.ExecuteNonQuery();
    }

    public WorkInterval? GetOpenInterval(Guid sessionId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, WorkSessionId, StartedAt, EndedAt
            FROM WorkInterval
            WHERE WorkSessionId = @sessionId AND EndedAt IS NULL
            ORDER BY StartedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@sessionId", sessionId.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadInterval(reader) : null;
    }

    public IReadOnlyList<WorkSession> GetSessionsForLocalDay(DateTimeOffset day)
    {
        var local = day.ToLocalTime();
        var start = new DateTimeOffset(local.Date, local.Offset);
        var end = start.AddDays(1);

        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskName, Mode, StartedAt, FinishedAt, CountdownDurationTicks,
                   State, Note, LastHeartbeatAt, CountdownEndsAt, CountdownRemainingTicks,
                   CountdownCompletedNotified
            FROM WorkSession
            WHERE StartedAt >= @start AND StartedAt < @end
            ORDER BY StartedAt;
            """;
        command.Parameters.AddWithValue("@start", start.ToString("O"));
        command.Parameters.AddWithValue("@end", end.ToString("O"));

        var results = new List<WorkSession>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadSession(reader));
        }

        return results;
    }

    public TimeSpan GetActiveDuration(Guid sessionId, DateTimeOffset? now = null)
    {
        return SessionCalculations.CalculateActiveDuration(GetIntervals(sessionId), now);
    }

    public void UpdateSessionDetails(WorkSession session, IReadOnlyList<WorkInterval> intervals)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        using (var update = connection.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText =
                """
                UPDATE WorkSession SET
                    TaskName = @taskName,
                    FinishedAt = @finishedAt,
                    State = @state,
                    Note = @note,
                    LastHeartbeatAt = @lastHeartbeatAt,
                    CountdownEndsAt = @countdownEndsAt,
                    CountdownRemainingTicks = @countdownRemainingTicks,
                    CountdownCompletedNotified = @countdownCompletedNotified,
                    StartedAt = @startedAt
                WHERE Id = @id;
                """;
            BindSessionUpdate(update, session);
            update.Parameters.AddWithValue("@startedAt", session.StartedAt.ToString("O"));
            update.ExecuteNonQuery();
        }

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM WorkInterval WHERE WorkSessionId = @sessionId;";
            delete.Parameters.AddWithValue("@sessionId", session.Id.ToString());
            delete.ExecuteNonQuery();
        }

        foreach (var interval in intervals)
        {
            InsertInterval(connection, tx, interval);
        }

        tx.Commit();
    }

    public void DeleteSession(Guid sessionId)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        using (var deleteIntervals = connection.CreateCommand())
        {
            deleteIntervals.Transaction = tx;
            deleteIntervals.CommandText = "DELETE FROM WorkInterval WHERE WorkSessionId = @id;";
            deleteIntervals.Parameters.AddWithValue("@id", sessionId.ToString());
            deleteIntervals.ExecuteNonQuery();
        }

        using (var deleteSession = connection.CreateCommand())
        {
            deleteSession.Transaction = tx;
            deleteSession.CommandText = "DELETE FROM WorkSession WHERE Id = @id;";
            deleteSession.Parameters.AddWithValue("@id", sessionId.ToString());
            deleteSession.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void InsertSession(SqliteConnection connection, SqliteTransaction? tx, WorkSession session)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText =
            """
            INSERT INTO WorkSession (
                Id, TaskName, Mode, StartedAt, FinishedAt, CountdownDurationTicks,
                State, Note, LastHeartbeatAt, CountdownEndsAt, CountdownRemainingTicks,
                CountdownCompletedNotified)
            VALUES (
                @id, @taskName, @mode, @startedAt, @finishedAt, @countdownDurationTicks,
                @state, @note, @lastHeartbeatAt, @countdownEndsAt, @countdownRemainingTicks,
                @countdownCompletedNotified);
            """;
        command.Parameters.AddWithValue("@id", session.Id.ToString());
        command.Parameters.AddWithValue("@taskName", session.TaskName);
        command.Parameters.AddWithValue("@mode", (int)session.Mode);
        command.Parameters.AddWithValue("@startedAt", session.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("@finishedAt", (object?)session.FinishedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@countdownDurationTicks", (object?)session.CountdownDuration?.Ticks ?? DBNull.Value);
        command.Parameters.AddWithValue("@state", (int)session.State);
        command.Parameters.AddWithValue("@note", (object?)session.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastHeartbeatAt", (object?)session.LastHeartbeatAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@countdownEndsAt", (object?)session.CountdownEndsAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@countdownRemainingTicks", (object?)session.CountdownRemaining?.Ticks ?? DBNull.Value);
        command.Parameters.AddWithValue("@countdownCompletedNotified", session.CountdownCompletedNotified ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private static void InsertInterval(SqliteConnection connection, SqliteTransaction? tx, WorkInterval interval)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText =
            """
            INSERT INTO WorkInterval (Id, WorkSessionId, StartedAt, EndedAt)
            VALUES (@id, @sessionId, @startedAt, @endedAt);
            """;
        command.Parameters.AddWithValue("@id", interval.Id.ToString());
        command.Parameters.AddWithValue("@sessionId", interval.WorkSessionId.ToString());
        command.Parameters.AddWithValue("@startedAt", interval.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("@endedAt", (object?)interval.EndedAt?.ToString("O") ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void BindSessionUpdate(SqliteCommand command, WorkSession session)
    {
        command.Parameters.AddWithValue("@id", session.Id.ToString());
        command.Parameters.AddWithValue("@taskName", session.TaskName);
        command.Parameters.AddWithValue("@finishedAt", (object?)session.FinishedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@state", (int)session.State);
        command.Parameters.AddWithValue("@note", (object?)session.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastHeartbeatAt", (object?)session.LastHeartbeatAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@countdownEndsAt", (object?)session.CountdownEndsAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@countdownRemainingTicks", (object?)session.CountdownRemaining?.Ticks ?? DBNull.Value);
        command.Parameters.AddWithValue("@countdownCompletedNotified", session.CountdownCompletedNotified ? 1 : 0);
    }

    private static WorkSession ReadSession(SqliteDataReader reader)
    {
        return new WorkSession
        {
            Id = Guid.Parse(reader.GetString(0)),
            TaskName = reader.GetString(1),
            Mode = (TimerMode)reader.GetInt32(2),
            StartedAt = DateTimeOffset.Parse(reader.GetString(3)),
            FinishedAt = reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)),
            CountdownDuration = reader.IsDBNull(5) ? null : TimeSpan.FromTicks(reader.GetInt64(5)),
            State = (SessionState)reader.GetInt32(6),
            Note = reader.IsDBNull(7) ? null : reader.GetString(7),
            LastHeartbeatAt = reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
            CountdownEndsAt = reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)),
            CountdownRemaining = reader.IsDBNull(10) ? null : TimeSpan.FromTicks(reader.GetInt64(10)),
            CountdownCompletedNotified = reader.GetInt32(11) == 1
        };
    }

    private static WorkInterval ReadInterval(SqliteDataReader reader)
    {
        return new WorkInterval
        {
            Id = Guid.Parse(reader.GetString(0)),
            WorkSessionId = Guid.Parse(reader.GetString(1)),
            StartedAt = DateTimeOffset.Parse(reader.GetString(2)),
            EndedAt = reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3))
        };
    }
}
