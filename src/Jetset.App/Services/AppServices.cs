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
        TaskStore = new TaskStore(ConnectionFactory);
        ProjectStore = new ProjectStore(ConnectionFactory);
        Clock = new ClockService();
        Sessions = new SessionService(SessionStore, TaskStore, () => Clock.Now);
        Tasks = new TaskService(TaskStore, ProjectStore, () => Clock.Now);
        Projects = new ProjectService(ProjectStore, TaskStore, () => Clock.Now);
        WorkExecution = new WorkExecutionService(Sessions, Tasks);
        Analytics = new AnalyticsService(Sessions, Tasks, () => Clock.Now);
        Effort = new EffortService(Sessions, TaskStore);
        WorkTree = new WorkTreeService(TaskStore, ProjectStore);
        WorkItemConversion = new WorkItemConversionService(Tasks, Projects);
        Notifications = new NotificationService();
        Startup = new StartupService();
        Settings = new SettingsService(SettingsStore, Startup);
        TreeState = new TreeStateStore(ConnectionFactory);
        SystemIdle = new SystemIdleService();
        IdleAutoPause = new IdleAutoPauseController(Sessions, WorkExecution, SystemIdle, () => Settings.Settings);
        Tray = new TrayService(Notifications);
    }

    public SqliteConnectionFactory ConnectionFactory { get; }
    public SessionStore SessionStore { get; }
    public SettingsStore SettingsStore { get; }
    public TaskStore TaskStore { get; }
    public ProjectStore ProjectStore { get; }
    public ClockService Clock { get; }
    public SessionService Sessions { get; }
    public TaskService Tasks { get; }
    public WorkExecutionService WorkExecution { get; }
    public AnalyticsService Analytics { get; }
    public EffortService Effort { get; }
    public WorkTreeService WorkTree { get; }
    public WorkItemConversionService WorkItemConversion { get; }
    public ProjectService Projects { get; }
    public NotificationService Notifications { get; }
    public StartupService Startup { get; }
    public SettingsService Settings { get; }
    public ITreeStateStore TreeState { get; }
    public ISystemIdleService SystemIdle { get; }
    public IdleAutoPauseController IdleAutoPause { get; }
    public TrayService Tray { get; }
}
