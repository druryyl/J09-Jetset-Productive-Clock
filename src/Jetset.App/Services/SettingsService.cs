using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

public sealed class SettingsService
{
    private readonly SettingsStore _store;
    private readonly StartupService _startupService;

    public SettingsService(SettingsStore store, StartupService startupService)
    {
        _store = store;
        _startupService = startupService;
        Settings = _store.Load();
        Settings.StartWithWindows = _startupService.IsEnabled();
    }

    public AppSettings Settings { get; private set; }

    public event EventHandler? SettingsChanged;

    public void Save(AppSettings settings)
    {
        Settings = settings;
        _store.Save(settings);
        _startupService.SetEnabled(settings.StartWithWindows);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Settings);
        Save(Settings);
    }
}
