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
    private Guid? _milestoneId;
    private string? _milestoneName;

    public TaskListItemViewModel(WorkTask task, string? projectName = null, string? milestoneName = null)
    {
        Task = task;
        _title = task.Title;
        _status = task.Status;
        _notes = task.Notes ?? string.Empty;
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
}
