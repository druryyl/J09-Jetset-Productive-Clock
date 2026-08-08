using Jetset.App.Models;
using Jetset.App.Persistence;

namespace Jetset.App.Services;

/// <summary>
/// Application composition root — concrete services wired once at startup.
/// </summary>
public sealed class AppServices
{
    public AppServices(string? databasePath = null)
    {
        SQLitePCL.Batteries.Init();

        ConnectionFactory = databasePath is null
            ? SqliteConnectionFactory.CreateDefault()
            : new SqliteConnectionFactory(databasePath);

        new SchemaInitializer(ConnectionFactory).Initialize();

        SessionStore = new SessionStore(ConnectionFactory);
        SettingsStore = new SettingsStore(ConnectionFactory);
        Clock = new ClockService();
        Sessions = new SessionService(SessionStore, () => Clock.Now);
        Notifications = new NotificationService();
        Startup = new StartupService();
        Settings = new SettingsService(SettingsStore, Startup);
        Tray = new TrayService(Notifications);
    }

    public SqliteConnectionFactory ConnectionFactory { get; }
    public SessionStore SessionStore { get; }
    public SettingsStore SettingsStore { get; }
    public ClockService Clock { get; }
    public SessionService Sessions { get; }
    public NotificationService Notifications { get; }
    public StartupService Startup { get; }
    public SettingsService Settings { get; }
    public TrayService Tray { get; }
}
