using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public sealed class ContextSnapshotItemViewModel : ObservableObject
{
    public ContextSnapshotItemViewModel(ContextSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public ContextSnapshot Snapshot { get; }

    public Guid Id => Snapshot.Id;

    public DateTimeOffset CreatedAt => Snapshot.CreatedAt;

    public string CreatedAtDisplay => Snapshot.CreatedAt.ToString("g");

    public string? CurrentStatus => Snapshot.CurrentStatus;

    public string? LastProgress => Snapshot.LastProgress;

    public string? NextAction => Snapshot.NextAction;

    public string? Blocker => Snapshot.Blocker;

    public string? Notes => Snapshot.Notes;

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool HasBlocker => !string.IsNullOrWhiteSpace(Blocker);

    public string Summary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(NextAction))
            {
                return NextAction.Trim();
            }

            if (!string.IsNullOrWhiteSpace(CurrentStatus))
            {
                return CurrentStatus.Trim();
            }

            if (!string.IsNullOrWhiteSpace(LastProgress))
            {
                return LastProgress.Trim();
            }

            return string.Empty;
        }
    }

    public string BlockerDisplay => HasBlocker ? Blocker!.Trim() : string.Empty;
}
