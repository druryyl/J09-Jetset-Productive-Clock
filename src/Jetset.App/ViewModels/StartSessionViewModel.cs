using System.Collections.ObjectModel;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.ViewModels;

public sealed class TaskPickerItem
{
    public TaskPickerItem(Guid id, string title)
    {
        Id = id;
        Title = title;
    }

    public Guid Id { get; }

    public string Title { get; }

    public override string ToString() => Title;
}

public sealed class StartSessionViewModel : ObservableObject
{
    private readonly TaskService? _tasks;
    private Guid? _selectedTaskId;
    private string _newTaskTitle = string.Empty;
    private TimerMode _mode = TimerMode.Stopwatch;
    private int _selectedPresetMinutes = 25;
    private bool _useCustomDuration;
    private string _customMinutes = "25";

    public StartSessionViewModel(TaskService? tasks = null)
    {
        _tasks = tasks;
        Presets = [5, 15, 25, 45, 60];
        AvailableTasks = new ObservableCollection<TaskPickerItem>();
    }

    public IReadOnlyList<int> Presets { get; }

    public ObservableCollection<TaskPickerItem> AvailableTasks { get; }

    public Guid? SelectedTaskId
    {
        get => _selectedTaskId;
        set => SetProperty(ref _selectedTaskId, value);
    }

    public string NewTaskTitle
    {
        get => _newTaskTitle;
        set => SetProperty(ref _newTaskTitle, value);
    }

    public TimerMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsCountdown));
                OnPropertyChanged(nameof(IsStopwatch));
            }
        }
    }

    public bool IsStopwatch
    {
        get => Mode == TimerMode.Stopwatch;
        set
        {
            if (value)
            {
                Mode = TimerMode.Stopwatch;
            }
        }
    }

    public bool IsCountdown
    {
        get => Mode == TimerMode.Countdown;
        set
        {
            if (value)
            {
                Mode = TimerMode.Countdown;
            }
        }
    }

    public int SelectedPresetMinutes
    {
        get => _selectedPresetMinutes;
        set
        {
            if (SetProperty(ref _selectedPresetMinutes, value))
            {
                UseCustomDuration = false;
            }
        }
    }

    public bool UseCustomDuration
    {
        get => _useCustomDuration;
        set
        {
            if (SetProperty(ref _useCustomDuration, value) && value)
            {
                OnPropertyChanged(nameof(SelectedPresetMinutes));
            }
        }
    }

    public string CustomMinutes
    {
        get => _customMinutes;
        set => SetProperty(ref _customMinutes, value);
    }

    public void RefreshTaskList()
    {
        if (_tasks is null)
        {
            return;
        }

        AvailableTasks.Clear();
        foreach (var task in _tasks.ListByStatuses([TaskStatus.Ready, TaskStatus.Waiting])
            .OrderByDescending(t => t.LastWorkedAt ?? t.UpdatedAt))
        {
            AvailableTasks.Add(new TaskPickerItem(task.Id, task.Title));
        }
    }

    public void Reset()
    {
        SelectedTaskId = null;
        NewTaskTitle = string.Empty;
        Mode = TimerMode.Stopwatch;
        SelectedPresetMinutes = 25;
        UseCustomDuration = false;
        CustomMinutes = "25";
    }

    public bool TryBuild(
        out Guid? selectedTaskId,
        out string? newTaskTitle,
        out TimerMode mode,
        out TimeSpan? duration,
        out string? error)
    {
        selectedTaskId = SelectedTaskId;
        newTaskTitle = NewTaskTitle.Trim();
        if (string.IsNullOrWhiteSpace(newTaskTitle))
        {
            newTaskTitle = null;
        }

        mode = Mode;
        duration = null;
        error = null;

        if (selectedTaskId is null && newTaskTitle is null)
        {
            error = "Select a task or enter a new task name.";
            return false;
        }

        if (mode == TimerMode.Countdown)
        {
            int minutes;
            if (UseCustomDuration)
            {
                if (!int.TryParse(CustomMinutes.Trim(), out minutes) || minutes <= 0)
                {
                    error = "Enter a valid custom duration in minutes.";
                    return false;
                }
            }
            else
            {
                minutes = SelectedPresetMinutes;
            }

            duration = TimeSpan.FromMinutes(minutes);
        }

        return true;
    }
}
