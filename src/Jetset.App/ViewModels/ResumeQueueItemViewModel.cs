using Jetset.App.Helpers;

namespace Jetset.App.ViewModels;

public sealed class ResumeQueueItemViewModel : ObservableObject
{
    private string _durationText = string.Empty;
    private string _statusText = "Waiting";
    private string? _currentStatus;
    private string? _lastProgress;
    private string? _nextAction;
    private string? _blocker;

    public ResumeQueueItemViewModel(
        Guid taskId,
        Guid? sessionId,
        string title,
        string? currentStatus,
        string? lastProgress,
        string? nextAction,
        string? blocker,
        RelayCommand resumeCommand)
    {
        TaskId = taskId;
        SessionId = sessionId;
        Title = title;
        CurrentStatus = currentStatus;
        LastProgress = lastProgress;
        NextAction = nextAction;
        Blocker = blocker;
        ResumeCommand = resumeCommand;
    }

    public Guid TaskId { get; }

    public Guid? SessionId { get; }

    public string Title { get; }

    public string? CurrentStatus
    {
        get => _currentStatus;
        set
        {
            if (SetProperty(ref _currentStatus, value))
            {
                OnPropertyChanged(nameof(HasCurrentStatus));
            }
        }
    }

    public string? LastProgress
    {
        get => _lastProgress;
        set
        {
            if (SetProperty(ref _lastProgress, value))
            {
                OnPropertyChanged(nameof(HasLastProgress));
            }
        }
    }

    public string? NextAction
    {
        get => _nextAction;
        set
        {
            if (SetProperty(ref _nextAction, value))
            {
                OnPropertyChanged(nameof(HasNextAction));
            }
        }
    }

    public string? Blocker
    {
        get => _blocker;
        set
        {
            if (SetProperty(ref _blocker, value))
            {
                OnPropertyChanged(nameof(HasBlocker));
            }
        }
    }

    public bool HasNextAction => !string.IsNullOrWhiteSpace(NextAction);

    public bool HasCurrentStatus => !string.IsNullOrWhiteSpace(CurrentStatus);

    public bool HasLastProgress => !string.IsNullOrWhiteSpace(LastProgress);

    public bool HasBlocker => !string.IsNullOrWhiteSpace(Blocker);

    public RelayCommand ResumeCommand { get; }

    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}
