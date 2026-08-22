using Jetset.App.Helpers;
using Jetset.App.Services;

namespace Jetset.App.ViewModels;

public sealed class SettingsAreaViewModel
{
    public SettingsAreaViewModel(AppServices services)
    {
        General = new SettingsViewModel(services.Settings);
        Analytics = new AnalyticsViewModel(services);
        OpenHistoryCommand = new RelayCommand(() => OpenHistoryRequested?.Invoke(this, EventArgs.Empty));
    }

    public SettingsViewModel General { get; }

    public AnalyticsViewModel Analytics { get; }

    public RelayCommand OpenHistoryCommand { get; }

    public event EventHandler? OpenHistoryRequested;
}
