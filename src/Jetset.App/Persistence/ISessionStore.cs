using Jetset.App.Models;

namespace Jetset.App.Persistence;

public interface ISessionStore
{
    WorkSession? GetActiveSession();

    IReadOnlyList<WorkSession> GetInProgressSessions();

    IReadOnlyList<WorkInterval> GetIntervals(Guid sessionId);

    void SaveNewSession(WorkSession session, WorkInterval firstInterval);

    void UpdateSession(WorkSession session);

    void InsertInterval(WorkInterval interval);

    void CloseInterval(Guid intervalId, DateTimeOffset endedAt);

    WorkInterval? GetOpenInterval(Guid sessionId);

    IReadOnlyList<WorkSession> GetSessionsForLocalDay(DateTimeOffset day);

    TimeSpan GetActiveDuration(Guid sessionId, DateTimeOffset? now = null);

    void UpdateSessionDetails(WorkSession session, IReadOnlyList<WorkInterval> intervals);

    void DeleteSession(Guid sessionId);
}
