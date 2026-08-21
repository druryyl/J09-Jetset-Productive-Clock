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
        MilestoneStore = new MilestoneStore(ConnectionFactory);
        ContextSnapshotStore = new ContextSnapshotStore(ConnectionFactory);
        TaskSwitchEventStore = new TaskSwitchEventStore(ConnectionFactory);
        Clock = new ClockService();
        Sessions = new SessionService(SessionStore, TaskStore, TaskSwitchEventStore, () => Clock.Now);
        Tasks = new TaskService(TaskStore, ProjectStore, MilestoneStore, () => Clock.Now);
        Projects = new ProjectService(ProjectStore, TaskStore, MilestoneStore, () => Clock.Now);
        Milestones = new MilestoneService(MilestoneStore, ProjectStore, TaskStore, () => Clock.Now);
        ContextSnapshots = new ContextSnapshotService(ContextSnapshotStore, TaskStore, () => Clock.Now);
        WorkExecution = new WorkExecutionService(Sessions, Tasks, ContextSnapshots);
        ResumeQueue = new ResumeQueueService(Tasks, Sessions);
        Analytics = new AnalyticsService(Sessions, Tasks, Projects, TaskSwitchEventStore, () => Clock.Now);
        Notifications = new NotificationService();
        Startup = new StartupService();
        Settings = new SettingsService(SettingsStore, Startup);
        SystemIdle = new SystemIdleService();
        IdleAutoPause = new IdleAutoPauseController(Sessions, WorkExecution, SystemIdle, () => Settings.Settings);
        Tray = new TrayService(Notifications);
    }

    public SqliteConnectionFactory ConnectionFactory { get; }
    public SessionStore SessionStore { get; }
    public SettingsStore SettingsStore { get; }
    public TaskStore TaskStore { get; }
    public ProjectStore ProjectStore { get; }
    public MilestoneStore MilestoneStore { get; }
    public ContextSnapshotStore ContextSnapshotStore { get; }
    public TaskSwitchEventStore TaskSwitchEventStore { get; }
    public ClockService Clock { get; }
    public SessionService Sessions { get; }
    public TaskService Tasks { get; }
    public WorkExecutionService WorkExecution { get; }
    public ResumeQueueService ResumeQueue { get; }
    public AnalyticsService Analytics { get; }
    public ProjectService Projects { get; }
    public MilestoneService Milestones { get; }
    public ContextSnapshotService ContextSnapshots { get; }
    public NotificationService Notifications { get; }
    public StartupService Startup { get; }
    public SettingsService Settings { get; }
    public ISystemIdleService SystemIdle { get; }
    public IdleAutoPauseController IdleAutoPause { get; }
    public TrayService Tray { get; }
}
