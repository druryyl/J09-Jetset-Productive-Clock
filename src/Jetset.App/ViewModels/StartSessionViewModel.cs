using Jetset.App.Helpers;
using Jetset.App.Models;

namespace Jetset.App.ViewModels;

public sealed class StartSessionViewModel : ObservableObject
{
    private string _taskName = string.Empty;
    private TimerMode _mode = TimerMode.Stopwatch;
    private int _selectedPresetMinutes = 25;
    private bool _useCustomDuration;
    private string _customMinutes = "25";

    public StartSessionViewModel()
    {
        Presets = [5, 15, 25, 45, 60];
    }

    public IReadOnlyList<int> Presets { get; }

    public string TaskName
    {
        get => _taskName;
        set => SetProperty(ref _taskName, value);
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

    public void Reset()
    {
        TaskName = string.Empty;
        Mode = TimerMode.Stopwatch;
        SelectedPresetMinutes = 25;
        UseCustomDuration = false;
        CustomMinutes = "25";
    }

    public bool TryBuild(out string taskName, out TimerMode mode, out TimeSpan? duration, out string? error)
    {
        taskName = TaskName.Trim();
        mode = Mode;
        duration = null;
        error = null;

        if (string.IsNullOrWhiteSpace(taskName))
        {
            error = "Enter a task name.";
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
