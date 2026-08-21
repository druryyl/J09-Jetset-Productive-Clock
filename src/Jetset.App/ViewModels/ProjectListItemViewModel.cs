using System.Globalization;
using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public sealed class ProjectListItemViewModel : ObservableObject
{
    private string _name;
    private bool _hasDeadline;
    private DateTime? _deadlineDate;
    private int _taskCount;

    public ProjectListItemViewModel(ProjectSummary summary)
        : this(summary.Project, summary.TaskCount)
    {
    }

    public ProjectListItemViewModel(Project project, int taskCount)
    {
        Project = project;
        _name = project.Name;
        _hasDeadline = project.Deadline is not null;
        _deadlineDate = project.Deadline?.ToDateTime(TimeOnly.MinValue);
        _taskCount = taskCount;
    }

    public Project Project { get; }

    public Guid Id => Project.Id;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool HasDeadline
    {
        get => _hasDeadline;
        set
        {
            if (SetProperty(ref _hasDeadline, value))
            {
                OnPropertyChanged(nameof(DeadlineText));
                if (!value)
                {
                    DeadlineDate = null;
                }
                else if (DeadlineDate is null)
                {
                    DeadlineDate = DateTime.Today;
                }
            }
        }
    }

    public DateTime? DeadlineDate
    {
        get => _deadlineDate;
        set
        {
            if (SetProperty(ref _deadlineDate, value))
            {
                OnPropertyChanged(nameof(DeadlineText));
            }
        }
    }

    public DateOnly? Deadline =>
        HasDeadline && DeadlineDate is { } date
            ? DateOnly.FromDateTime(date)
            : null;

    public int TaskCount
    {
        get => _taskCount;
        set
        {
            if (SetProperty(ref _taskCount, value))
            {
                OnPropertyChanged(nameof(TaskCountText));
            }
        }
    }

    public string TaskCountText => TaskCount == 1 ? "1 task" : $"{TaskCount} tasks";

    public string DeadlineText =>
        Deadline is { } d
            ? $"Due {d.ToString("MMM d", CultureInfo.CurrentCulture)}"
            : string.Empty;
}
