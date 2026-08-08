using System.Windows;
using Jetset.App.Services;

namespace Jetset.App;

public partial class App : System.Windows.Application
{
    private AppServices? _services;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = new AppServices();
        ThemeManager.Apply(_services.Settings.Settings.UseDarkTheme);

        _services.Tray.Initialize();
        _services.Tray.ShowWindowRequested += (_, _) => _mainWindow?.ShowFromTray();
        _services.Tray.ExitRequested += (_, _) =>
        {
            _mainWindow?.RequestExit();
            Shutdown();
        };

        _mainWindow = new MainWindow(_services);
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Tray.Dispose();
        base.OnExit(e);
    }
}
