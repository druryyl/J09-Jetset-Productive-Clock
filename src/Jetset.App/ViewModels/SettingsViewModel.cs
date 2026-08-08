using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private bool _alwaysOnTop;
    private bool _use24HourClock;
    private bool _showSeconds;
    private bool _soundOnCountdownComplete;
    private bool _startWithWindows;
    private bool _useDarkTheme;
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

        SaveCommand = new RelayCommand(Save);
    }

    public RelayCommand SaveCommand { get; }

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
        _settingsService.Save(current);
        Message = "Settings saved.";
    }
}
