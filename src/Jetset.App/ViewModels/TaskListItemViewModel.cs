using Jetset.App.Helpers;
using Jetset.App.Models;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.ViewModels;

public sealed class TaskListItemViewModel : ObservableObject
{
    private string _title;
    private TaskStatus _status;
    private string _notes;
    private Guid? _projectId;
    private string? _projectName;
    private TaskOrigin _origin;
    private bool _hasPausedSession;
    private bool _isActiveSession;
    private bool _showSwitchActions;

    public TaskListItemViewModel(WorkTask task, string? projectName = null)
    {
        Task = task;
        _title = task.Title;
        _status = task.Status;
        _notes = task.Notes ?? string.Empty;
        _projectId = task.ProjectId;
        _projectName = projectName;
        _origin = task.Origin;
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
                OnPropertyChanged(nameof(SearchSubtitle));
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

    public Guid? ProjectId
    {
        get => _projectId;
        set
        {
            if (SetProperty(ref _projectId, value))
            {
                OnPropertyChanged(nameof(HasProject));
                OnPropertyChanged(nameof(ProjectDisplay));
                OnPropertyChanged(nameof(SearchSubtitle));
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
                OnPropertyChanged(nameof(SearchSubtitle));
            }
        }
    }

    public bool HasProject => ProjectId is not null;

    public string ProjectDisplay =>
        string.IsNullOrWhiteSpace(ProjectName) ? string.Empty : ProjectName;

    public string StatusText => Status.ToString();

    public string SearchSubtitle =>
        HasProject && !string.IsNullOrWhiteSpace(ProjectDisplay)
            ? $"{StatusText} · {ProjectDisplay}"
            : StatusText;

    public TaskOrigin Origin
    {
        get => _origin;
        set
        {
            if (SetProperty(ref _origin, value))
            {
                OnPropertyChanged(nameof(OriginText));
                OnPropertyChanged(nameof(IsPlanned));
            }
        }
    }

    public string OriginText => Origin.ToString();

    public bool IsPlanned => Origin == TaskOrigin.Planned;

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

    public bool CanStartWork =>
        !IsTerminal && !IsActiveSession && !HasPausedSession && TaskStatusRules.CanStart(Status);

    public bool CanResumeWork => !IsTerminal && HasPausedSession && !IsActiveSession;

    public bool IsWorkActive => IsActiveSession;

    public bool ShowSwitchActions
    {
        get => _showSwitchActions;
        set => SetProperty(ref _showSwitchActions, value);
    }
}
