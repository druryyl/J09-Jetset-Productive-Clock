using Jetset.App.Helpers;
using Jetset.App.Models;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.ViewModels;

public sealed class TaskListItemViewModel : ObservableObject
{
    private string _title;
    private TaskStatus _status;
    private string _notes;
    private string _currentStatus;
    private string _lastProgress;
    private string _nextAction;
    private string _blocker;
    private Guid? _projectId;
    private string? _projectName;
    private Guid? _milestoneId;
    private string? _milestoneName;
    private bool _hasPausedSession;
    private bool _isActiveSession;

    public TaskListItemViewModel(WorkTask task, string? projectName = null, string? milestoneName = null)
    {
        Task = task;
        _title = task.Title;
        _status = task.Status;
        _notes = task.Notes ?? string.Empty;
        _currentStatus = task.CurrentStatus ?? string.Empty;
        _lastProgress = task.LastProgress ?? string.Empty;
        _nextAction = task.NextAction ?? string.Empty;
        _blocker = task.Blocker ?? string.Empty;
        _projectId = task.ProjectId;
        _projectName = projectName;
        _milestoneId = task.MilestoneId;
        _milestoneName = milestoneName;
    }

    public WorkTask Task { get; }

    public Guid Id => Task.Id;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public TaskStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsTerminal));
                OnPropertyChanged(nameof(CanReopen));
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string CurrentStatus
    {
        get => _currentStatus;
        set
        {
            if (SetProperty(ref _currentStatus, value))
            {
                RaiseContextSummaryChanged();
            }
        }
    }

    public string LastProgress
    {
        get => _lastProgress;
        set
        {
            if (SetProperty(ref _lastProgress, value))
            {
                RaiseContextSummaryChanged();
            }
        }
    }

    public string NextAction
    {
        get => _nextAction;
        set
        {
            if (SetProperty(ref _nextAction, value))
            {
                RaiseContextSummaryChanged();
            }
        }
    }

    public string Blocker
    {
        get => _blocker;
        set
        {
            if (SetProperty(ref _blocker, value))
            {
                OnPropertyChanged(nameof(HasBlocker));
                OnPropertyChanged(nameof(BlockerDisplay));
            }
        }
    }

    public bool HasContextSummary => !string.IsNullOrWhiteSpace(ContextSummary);

    public bool HasCurrentStatusDisplay => !string.IsNullOrWhiteSpace(_currentStatus);

    public bool HasLastProgressDisplay => !string.IsNullOrWhiteSpace(_lastProgress);

    public bool HasNextActionDisplay => !string.IsNullOrWhiteSpace(_nextAction);

    public bool HasBlocker => !string.IsNullOrWhiteSpace(Blocker);

    public string ContextSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_nextAction))
            {
                return _nextAction.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_currentStatus))
            {
                return _currentStatus.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_lastProgress))
            {
                return _lastProgress.Trim();
            }

            return string.Empty;
        }
    }

    public string BlockerDisplay => HasBlocker ? Blocker.Trim() : string.Empty;

    private void RaiseContextSummaryChanged()
    {
        OnPropertyChanged(nameof(ContextSummary));
        OnPropertyChanged(nameof(HasContextSummary));
    }

    public Guid? ProjectId
    {
        get => _projectId;
        set
        {
            if (SetProperty(ref _projectId, value))
            {
                OnPropertyChanged(nameof(HasProject));
                OnPropertyChanged(nameof(ProjectDisplay));
            }
        }
    }

    public string? ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
            {
                OnPropertyChanged(nameof(HasProject));
                OnPropertyChanged(nameof(ProjectDisplay));
            }
        }
    }

    public Guid? MilestoneId
    {
        get => _milestoneId;
        set
        {
            if (SetProperty(ref _milestoneId, value))
            {
                OnPropertyChanged(nameof(HasMilestone));
                OnPropertyChanged(nameof(MilestoneDisplay));
            }
        }
    }

    public string? MilestoneName
    {
        get => _milestoneName;
        set
        {
            if (SetProperty(ref _milestoneName, value))
            {
                OnPropertyChanged(nameof(HasMilestone));
                OnPropertyChanged(nameof(MilestoneDisplay));
            }
        }
    }

    public bool HasProject => ProjectId is not null;

    public bool HasMilestone => MilestoneId is not null;

    public string ProjectDisplay =>
        string.IsNullOrWhiteSpace(ProjectName) ? string.Empty : ProjectName;

    public string MilestoneDisplay =>
        string.IsNullOrWhiteSpace(MilestoneName) ? string.Empty : MilestoneName;

    public string StatusText => Status.ToString();

    public bool IsTerminal => TaskStatusRules.IsTerminal(Status);

    public bool CanReopen => IsTerminal;

    public bool HasPausedSession
    {
        get => _hasPausedSession;
        set
        {
            if (SetProperty(ref _hasPausedSession, value))
            {
                OnPropertyChanged(nameof(CanStartWork));
                OnPropertyChanged(nameof(CanResumeWork));
            }
        }
    }

    public bool IsActiveSession
    {
        get => _isActiveSession;
        set
        {
            if (SetProperty(ref _isActiveSession, value))
            {
                OnPropertyChanged(nameof(CanStartWork));
                OnPropertyChanged(nameof(CanResumeWork));
                OnPropertyChanged(nameof(IsWorkActive));
            }
        }
    }

    public bool CanStartWork => !IsTerminal && !IsActiveSession && !HasPausedSession;

    public bool CanResumeWork => !IsTerminal && HasPausedSession && !IsActiveSession;

    public bool IsWorkActive => IsActiveSession;
}
