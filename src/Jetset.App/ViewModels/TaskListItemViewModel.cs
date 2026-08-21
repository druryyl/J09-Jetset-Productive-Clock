using Jetset.App.Helpers;
using Jetset.App.Models;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.ViewModels;

public sealed class TaskListItemViewModel : ObservableObject
{
    private string _title;
    private TaskStatus _status;
    private string _notes;

    public TaskListItemViewModel(WorkTask task)
    {
        Task = task;
        _title = task.Title;
        _status = task.Status;
        _notes = task.Notes ?? string.Empty;
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

    public string StatusText => Status.ToString();
}
