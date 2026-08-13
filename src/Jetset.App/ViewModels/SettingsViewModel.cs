using System.Collections.ObjectModel;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private static readonly int[] TimeoutChoices = [1, 3, 5, 10, 15, 30];

    private readonly SettingsService _settingsService;
    private bool _alwaysOnTop;
    private bool _use24HourClock;
    private bool _showSeconds;
    private bool _soundOnCountdownComplete;
    private bool _startWithWindows;
    private bool _useDarkTheme;
    private bool _autoPauseWhenIdle;
    private int _idleTimeoutMinutes;
    private bool _autoResumeAfterIdle;
    private string? _message;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var s = settingsService.Settings;
        _alwaysOnTop = s.AlwaysOnTop;
        _use24HourClock = s.Use24HourClock;
        _showSeconds = s.ShowSeconds;
        _soundOnCountdownComplete = s.SoundOnCountdownComplete;
        _startWithWindows = s.StartWithWindows;
        _useDarkTheme = s.UseDarkTheme;
        _autoPauseWhenIdle = s.AutoPauseWhenIdle;
        _idleTimeoutMinutes = NormalizeTimeout(s.IdleTimeoutMinutes);
        _autoResumeAfterIdle = s.AutoResumeAfterIdle;

        IdleTimeoutOptions = new ObservableCollection<int>(TimeoutChoices);
        SaveCommand = new RelayCommand(Save);
    }

    public RelayCommand SaveCommand { get; }

    public ObservableCollection<int> IdleTimeoutOptions { get; }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => SetProperty(ref _alwaysOnTop, value);
    }

    public bool Use24HourClock
    {
        get => _use24HourClock;
        set => SetProperty(ref _use24HourClock, value);
    }

    public bool ShowSeconds
    {
        get => _showSeconds;
        set => SetProperty(ref _showSeconds, value);
    }

    public bool SoundOnCountdownComplete
    {
        get => _soundOnCountdownComplete;
        set => SetProperty(ref _soundOnCountdownComplete, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool UseDarkTheme
    {
        get => _useDarkTheme;
        set => SetProperty(ref _useDarkTheme, value);
    }

    public bool AutoPauseWhenIdle
    {
        get => _autoPauseWhenIdle;
        set
        {
            if (SetProperty(ref _autoPauseWhenIdle, value))
            {
                OnPropertyChanged(nameof(IdleOptionsEnabled));
            }
        }
    }

    public int IdleTimeoutMinutes
    {
        get => _idleTimeoutMinutes;
        set => SetProperty(ref _idleTimeoutMinutes, NormalizeTimeout(value));
    }

    public bool AutoResumeAfterIdle
    {
        get => _autoResumeAfterIdle;
        set => SetProperty(ref _autoResumeAfterIdle, value);
    }

    public bool IdleOptionsEnabled => AutoPauseWhenIdle;

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    private void Save()
    {
        var current = _settingsService.Settings;
        current.AlwaysOnTop = AlwaysOnTop;
        current.Use24HourClock = Use24HourClock;
        current.ShowSeconds = ShowSeconds;
        current.SoundOnCountdownComplete = SoundOnCountdownComplete;
        current.StartWithWindows = StartWithWindows;
        current.UseDarkTheme = UseDarkTheme;
        current.AutoPauseWhenIdle = AutoPauseWhenIdle;
        current.IdleTimeoutMinutes = IdleTimeoutMinutes;
        current.AutoResumeAfterIdle = AutoResumeAfterIdle;
        _settingsService.Save(current);
        Message = "Settings saved.";
    }

    private static int NormalizeTimeout(int minutes)
    {
        var clamped = Math.Clamp(minutes, 1, 60);
        if (TimeoutChoices.Contains(clamped))
        {
            return clamped;
        }

        // Snap to nearest offered value for the ComboBox.
        return TimeoutChoices.OrderBy(v => Math.Abs(v - clamped)).First();
    }
}
