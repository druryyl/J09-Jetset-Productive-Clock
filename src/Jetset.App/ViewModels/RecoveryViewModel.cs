using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public sealed class RecoveryViewModel : ObservableObject
{
    public RecoveryViewModel(WorkSession session)
    {
        Session = session;
        TaskSummary = session.TaskName;
        StartedSummary = DurationFormatter.FormatTimeOfDay(session.StartedAt, use24Hour: true);
        LastKnownSummary = session.LastHeartbeatAt is { } hb
            ? DurationFormatter.FormatTimeOfDay(hb, use24Hour: true)
            : "unknown";
        LimitationText = session.LastHeartbeatAt is null
            ? "Exact stop time after an unexpected crash cannot be determined."
            : "Finish uses the last persisted heartbeat, not the crash instant.";
    }

    public WorkSession Session { get; }

    public string TaskSummary { get; }

    public string StartedSummary { get; }

    public string LastKnownSummary { get; }

    public string LimitationText { get; }
}
