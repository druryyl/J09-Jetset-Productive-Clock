using System.Collections.ObjectModel;
using System.Windows.Threading;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using Microsoft.Win32;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.ViewModels;

public sealed class FocusViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly IdleAutoPauseController _idleAutoPause;
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _heartbeatTimer;
    private readonly DispatcherTimer _idleTimer;

    private string _currentTime = string.Empty;
    private string _currentDate = string.Empty;
    private string _todayTotalText = "Today: 0m";
    private string _streakText = string.Empty;
    private string _taskName = string.Empty;
    private string _timerDisplay = string.Empty;
    private string _modeText = string.Empty;
    private string _statusText = string.Empty;
    private string _compactLine = string.Empty;
    private UiSessionState _uiState = UiSessionState.Idle;
    private bool _isCompact;
    private bool _alwaysOnTop;
    private bool _isOvertime;
    private bool _showStartPanel;
    private string? _validationMessage;
    private string _quickCaptureTitle = string.Empty;
    private Guid? _contextProjectId;
    private string _projectName = string.Empty;
    private string _projectContextText = string.Empty;

    public FocusViewModel(AppServices services)
    {
        _services = services;
        _idleAutoPause = services.IdleAutoPause;
        Settings = services.Settings.Settings;
        StartSession = new StartSessionViewModel(services.Tasks);
        ReadyTasks = new ObservableCollection<FocusTaskPickerItemViewModel>();
        WaitingTasks = new ObservableCollection<FocusTaskPickerItemViewModel>();

        StartWorkCommand = new RelayCommand(() =>
        {
            StartSession.RefreshTaskList();
            ShowStartPanel = true;
        }, () => !ShowStartPanel);
        CancelStartCommand = new RelayCommand(() =>
        {
            ShowStartPanel = false;
            StartSession.Reset();
            ValidationMessage = null;
        });
        ConfirmStartCommand = new RelayCommand(ConfirmStart);
        PauseCommand = new RelayCommand(Pause, () => UiState == UiSessionState.Running);
        ResumeCommand = new RelayCommand(Resume, () => UiState == UiSessionState.Paused);
        FinishCommand = new RelayCommand(Finish, () => UiState is UiSessionState.Running or UiSessionState.Paused);
        OpenHistoryCommand = new RelayCommand(() => OpenHistoryRequested?.Invoke(this, EventArgs.Empty));
        OpenSettingsCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        EditProjectContextCommand = new RelayCommand(
            () => EditProjectContextRequested?.Invoke(this, _contextProjectId!.Value),
            () => _contextProjectId is not null);
        ToggleCompactCommand = new RelayCommand(ToggleCompact);
        QuickCaptureCommand = new RelayCommand(QuickCapture, CanQuickCapture);
        StartTaskCommand = new RelayCommand(StartTaskFromPicker);
        SwitchAndMarkWaitingCommand = new RelayCommand(SwitchAndMarkWaitingFromPicker);

        IsCompact = Settings.CompactMode;
        AlwaysOnTop = Settings.AlwaysOnTop;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _uiTimer.Tick += (_, _) => RefreshDisplay();
        _uiTimer.Start();

        _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _heartbeatTimer.Tick += (_, _) => _services.Sessions.Heartbeat();
        _heartbeatTimer.Start();

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _idleTimer.Tick += (_, _) => _idleAutoPause.Evaluate();
        _idleTimer.Start();

        _services.Sessions.SessionChanged += (_, _) => RefreshFromSession();
        _idleAutoPause.StateChanged += (_, _) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                RefreshFromSession();
            }
            else
            {
                dispatcher.Invoke(RefreshFromSession);
            }
        };
        _services.Settings.SettingsChanged += (_, _) =>
        {
            Settings = _services.Settings.Settings;
            IsCompact = Settings.CompactMode;
            AlwaysOnTop = Settings.AlwaysOnTop;
            RefreshDisplay();
            OnPropertyChanged(nameof(Settings));
        };

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        RefreshFromSession();
        RefreshTaskLists();
        RefreshDisplay();
    }

    public AppSettings Settings { get; private set; }

    public StartSessionViewModel StartSession { get; }

    public ObservableCollection<FocusTaskPickerItemViewModel> ReadyTasks { get; }

    public ObservableCollection<FocusTaskPickerItemViewModel> WaitingTasks { get; }

    public RelayCommand StartWorkCommand { get; }
    public RelayCommand CancelStartCommand { get; }
    public RelayCommand ConfirmStartCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand FinishCommand { get; }
    public RelayCommand OpenHistoryCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand EditProjectContextCommand { get; }
    public RelayCommand ToggleCompactCommand { get; }
    public RelayCommand QuickCaptureCommand { get; }
    public RelayCommand StartTaskCommand { get; }
    public RelayCommand SwitchAndMarkWaitingCommand { get; }

    public event EventHandler? OpenHistoryRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler<Guid>? EditProjectContextRequested;
    public event EventHandler<WorkSession>? RecoveryNeeded;
    public event EventHandler? CompactModeChanged;
    public event EventHandler? QuickCaptureFocusRequested;

    public string CurrentTime
    {
        get => _currentTime;
        private set => SetProperty(ref _currentTime, value);
    }

    public string CurrentDate
    {
        get => _currentDate;
        private set => SetProperty(ref _currentDate, value);
    }

    public string TodayTotalText
    {
        get => _todayTotalText;
        private set => SetProperty(ref _todayTotalText, value);
    }

    public string StreakText
    {
        get => _streakText;
        private set
        {
            if (SetProperty(ref _streakText, value))
            {
                OnPropertyChanged(nameof(HasStreak));
            }
        }
    }

    public bool HasStreak => !string.IsNullOrEmpty(StreakText);

    public string TaskName
    {
        get => _taskName;
        private set => SetProperty(ref _taskName, value);
    }

    public string TimerDisplay
    {
        get => _timerDisplay;
        private set => SetProperty(ref _timerDisplay, value);
    }

    public string ModeText
    {
        get => _modeText;
        private set => SetProperty(ref _modeText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CompactLine
    {
        get => _compactLine;
        private set => SetProperty(ref _compactLine, value);
    }

    public UiSessionState UiState
    {
        get => _uiState;
        private set
        {
            if (SetProperty(ref _uiState, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsPaused));
                RaiseCommands();
            }
        }
    }

    public bool IsIdle => UiState == UiSessionState.Idle;
    public bool IsRunning => UiState == UiSessionState.Running;
    public bool IsPaused => UiState == UiSessionState.Paused;

    public bool IsCompact
    {
        get => _isCompact;
        set
        {
            if (SetProperty(ref _isCompact, value))
            {
                OnPropertyChanged(nameof(IsExpanded));
                CompactModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsExpanded => !IsCompact;

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => SetProperty(ref _alwaysOnTop, value);
    }

    public bool IsOvertime
    {
        get => _isOvertime;
        private set => SetProperty(ref _isOvertime, value);
    }

    public bool ShowStartPanel
    {
        get => _showStartPanel;
        set
        {
            if (SetProperty(ref _showStartPanel, value))
            {
                RaiseCommands();
            }
        }
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string QuickCaptureTitle
    {
        get => _quickCaptureTitle;
        set
        {
            if (SetProperty(ref _quickCaptureTitle, value))
            {
                QuickCaptureCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasReadyTasks => ReadyTasks.Count > 0;

    public bool HasWaitingTasks => WaitingTasks.Count > 0;

    public bool CanSwitchTasks => !IsIdle;

    public string StartWorkButtonText => CanSwitchTasks ? "Timer options" : "Start with timer";

    public bool HasProjectContext => _contextProjectId is not null;

    public bool HasProjectContextText => !string.IsNullOrWhiteSpace(ProjectContextText);

    public string ProjectName
    {
        get => _projectName;
        private set => SetProperty(ref _projectName, value);
    }

    public string ProjectContextText
    {
        get => _projectContextText;
        private set
        {
            if (SetProperty(ref _projectContextText, value))
            {
                OnPropertyChanged(nameof(HasProjectContextText));
            }
        }
    }

    public void ExitCompactMode()
    {
        if (!IsCompact)
        {
            return;
        }

        IsCompact = false;
        _services.Settings.Update(s => s.CompactMode = false);
    }

    public void RefreshTaskLists()
    {
        var projectNames = _services.Projects.ListProjects().ToDictionary(p => p.Id, p => p.Name);

        ReadyTasks.Clear();
        foreach (var task in OrderPickerTasks(_services.Tasks.ListByStatuses([TaskStatus.Ready])))
        {
            ReadyTasks.Add(CreatePickerItem(task, projectNames));
        }

        WaitingTasks.Clear();
        foreach (var task in OrderPickerTasks(_services.Tasks.ListByStatuses([TaskStatus.Waiting])))
        {
            WaitingTasks.Add(CreatePickerItem(task, projectNames));
        }

        OnPropertyChanged(nameof(HasReadyTasks));
        OnPropertyChanged(nameof(HasWaitingTasks));
    }

    public void RequestQuickCaptureFocus()
    {
        QuickCaptureFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    public void CheckRecovery()
    {
        var active = _services.Sessions.ActiveSession;
        if (active is not null)
        {
            RecoveryNeeded?.Invoke(this, active);
        }
    }

    public void ApplyRecoveryContinue()
    {
        _services.Sessions.ContinueRecovered();
        RefreshFromSession();
    }

    public void ApplyRecoveryFinishLastKnown()
    {
        _services.WorkExecution.FinishAtLastKnownActivity();
        _idleAutoPause.NotifySessionEnded();
        RefreshFromSession();
    }

    public void ApplyRecoveryDiscard()
    {
        _services.Sessions.Discard();
        _idleAutoPause.NotifySessionEnded();
        RefreshFromSession();
    }

    private void CompleteFinish(string? note = null)
    {
        _services.WorkExecution.FinishWork(note);
        _idleAutoPause.NotifySessionEnded();
        RefreshFromSession();
    }

    private void ConfirmStart()
    {
        ValidationMessage = null;
        if (!StartSession.TryBuild(
                out var selectedTaskId,
                out var newTaskTitle,
                out var mode,
                out var duration,
                out var error))
        {
            ValidationMessage = error;
            return;
        }

        try
        {
            Guid taskId;
            if (!string.IsNullOrWhiteSpace(newTaskTitle))
            {
                taskId = _services.Tasks.Create(newTaskTitle).Id;
            }
            else
            {
                taskId = selectedTaskId!.Value;
            }

            _idleAutoPause.NotifyManualResume();
            _services.WorkExecution.StartWork(taskId, mode, duration);
            ShowStartPanel = false;
            StartSession.Reset();
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void Pause()
    {
        try
        {
            _idleAutoPause.NotifyManualPause();
            _services.WorkExecution.PauseWork();
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void Resume()
    {
        try
        {
            _idleAutoPause.NotifyManualResume();
            _services.Sessions.Resume();
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void Finish()
    {
        try
        {
            CompleteFinish();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void ToggleCompact()
    {
        IsCompact = !IsCompact;
        _services.Settings.Update(s => s.CompactMode = IsCompact);
    }

    private void RefreshFromSession()
    {
        var session = _services.Sessions.ActiveSession;

        if (session is null)
        {
            _idleAutoPause.NotifySessionEnded();
            UiState = UiSessionState.Idle;
            TaskName = string.Empty;
            TimerDisplay = string.Empty;
            ModeText = string.Empty;
            StatusText = string.Empty;
            IsOvertime = false;
            RefreshProjectContext(null);
        }
        else
        {
            UiState = session.State == SessionState.Paused ? UiSessionState.Paused : UiSessionState.Running;
            TaskName = session.TaskName;
            ModeText = session.Mode == TimerMode.Countdown ? "Countdown" : "Stopwatch";
            StatusText = session.State == SessionState.Paused
                ? (_idleAutoPause.PausedByIdle ? "Paused (idle)" : "Paused")
                : "Running";
            RefreshProjectContext(session);
        }

        TodayTotalText = FormatTodayTotalText();
        StreakText = FormatStreakText();
        RefreshTaskLists();
        RaiseCommands();
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        var now = _services.Clock.Now;
        CurrentTime = DurationFormatter.FormatClock(now, Settings.Use24HourClock, Settings.ShowSeconds);
        CurrentDate = DurationFormatter.FormatDate(now);

        var session = _services.Sessions.ActiveSession;
        if (session is null)
        {
            CompactLine = string.Empty;
            TodayTotalText = FormatTodayTotalText();
            StreakText = FormatStreakText();
            return;
        }

        var active = _services.Sessions.GetActiveDuration(session.Id, now);

        if (session.Mode == TimerMode.Stopwatch)
        {
            TimerDisplay = DurationFormatter.FormatTimer(active);
            IsOvertime = false;
        }
        else
        {
            var remaining = SessionCalculations.GetCountdownRemaining(session, now);
            if (remaining < TimeSpan.Zero)
            {
                IsOvertime = true;
                TimerDisplay = DurationFormatter.FormatOvertime(remaining.Duration());
                MaybeNotifyCountdownComplete(session);
            }
            else
            {
                IsOvertime = false;
                TimerDisplay = DurationFormatter.FormatTimer(remaining);
            }
        }

        var glyph = session.State == SessionState.Paused ? "❚❚" : "▶";
        CompactLine = $"{glyph} {TimerDisplay}  {TaskName}";
    }

    private string FormatTodayTotalText()
    {
        var total = _services.Sessions.GetTodaysTotal();
        var formatted = DurationFormatter.FormatFriendly(total);
        var sessionCount = _services.Sessions.GetTodaysSessions()
            .Count(s => s.State != SessionState.Cancelled);

        return sessionCount > 0
            ? $"Today: {formatted} ({sessionCount} session{(sessionCount == 1 ? "" : "s")})"
            : $"Today: {formatted}";
    }

    private string FormatStreakText()
    {
        var streak = _services.Analytics.GetStreak();
        if (streak.CurrentStreak <= 0)
        {
            return string.Empty;
        }

        var current = streak.CurrentStreak == 1 ? "1 day" : $"{streak.CurrentStreak} days";
        var best = streak.LongestStreak == 1 ? "1 day" : $"{streak.LongestStreak} days";
        return $"Streak: {current} · Best: {best}";
    }

    private void MaybeNotifyCountdownComplete(WorkSession session)
    {
        if (session.CountdownCompletedNotified || session.State != SessionState.Running)
        {
            return;
        }

        _services.Sessions.MarkCountdownNotified();
        _services.Notifications.ShowBalloon("Jetset", $"Countdown finished: {session.TaskName}");
        _services.Notifications.ShowCountdownCompleted(session.TaskName, Settings.SoundOnCountdownComplete);
        StatusText = "Countdown complete — overtime";
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        void Handle()
        {
            if (e.Mode == PowerModes.Suspend)
            {
                _idleAutoPause.OnSessionLockedOrSuspended();
            }
            else if (e.Mode == PowerModes.Resume)
            {
                _idleAutoPause.OnSessionUnlockedOrResumed();
                RefreshDisplay();
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Handle();
        }
        else
        {
            dispatcher.Invoke(Handle);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        void Handle()
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                _idleAutoPause.OnSessionLockedOrSuspended();
            }
            else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
            {
                _idleAutoPause.OnSessionUnlockedOrResumed();
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Handle();
        }
        else
        {
            dispatcher.Invoke(Handle);
        }
    }

    private void RefreshProjectContext(WorkSession? session)
    {
        if (session is null)
        {
            _contextProjectId = null;
            ProjectName = string.Empty;
            ProjectContextText = string.Empty;
            OnPropertyChanged(nameof(HasProjectContext));
            EditProjectContextCommand.RaiseCanExecuteChanged();
            return;
        }

        var task = _services.Tasks.Get(session.TaskId);
        if (task?.ProjectId is not { } projectId)
        {
            _contextProjectId = null;
            ProjectName = string.Empty;
            ProjectContextText = string.Empty;
            OnPropertyChanged(nameof(HasProjectContext));
            EditProjectContextCommand.RaiseCanExecuteChanged();
            return;
        }

        var project = _services.Projects.Get(projectId);
        _contextProjectId = projectId;
        ProjectName = project?.Name ?? string.Empty;
        ProjectContextText = _services.Projects.GetContextText(projectId) ?? string.Empty;
        OnPropertyChanged(nameof(HasProjectContext));
        EditProjectContextCommand.RaiseCanExecuteChanged();
    }

    private bool CanQuickCapture() => !string.IsNullOrWhiteSpace(QuickCaptureTitle);

    private void QuickCapture()
    {
        ValidationMessage = null;
        try
        {
            _services.Tasks.CaptureToInbox(QuickCaptureTitle);
            QuickCaptureTitle = string.Empty;
            RefreshTaskLists();
            ValidationMessage = "Captured to Inbox.";
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void StartTaskFromPicker(object? parameter)
    {
        if (!TryParsePickerItem(parameter, out var taskId))
        {
            return;
        }

        StartTaskWithLeavingStatus(taskId, TaskStatus.Ready);
    }

    private void SwitchAndMarkWaitingFromPicker(object? parameter)
    {
        if (!TryParsePickerItem(parameter, out var taskId))
        {
            return;
        }

        StartTaskWithLeavingStatus(taskId, TaskStatus.Waiting);
    }

    private void StartTaskWithLeavingStatus(Guid taskId, TaskStatus leavingStatus)
    {
        ValidationMessage = null;
        try
        {
            _idleAutoPause.NotifyManualResume();
            _services.WorkExecution.StartWork(taskId, leavingStatus: leavingStatus);
            ShowStartPanel = false;
            StartSession.Reset();
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private static bool TryParsePickerItem(object? parameter, out Guid taskId)
    {
        switch (parameter)
        {
            case Guid id:
                taskId = id;
                return true;
            case FocusTaskPickerItemViewModel item:
                taskId = item.Id;
                return true;
            case string text when Guid.TryParse(text, out var parsed):
                taskId = parsed;
                return true;
            default:
                taskId = Guid.Empty;
                return false;
        }
    }

    private static IEnumerable<WorkTask> OrderPickerTasks(IReadOnlyList<WorkTask> tasks) =>
        tasks.OrderByDescending(t => t.LastWorkedAt ?? t.UpdatedAt);

    private static FocusTaskPickerItemViewModel CreatePickerItem(
        WorkTask task,
        Dictionary<Guid, string> projectNames)
    {
        string? projectName = null;
        if (task.ProjectId is { } projectId)
        {
            projectNames.TryGetValue(projectId, out projectName);
        }

        return new FocusTaskPickerItemViewModel(task.Id, task.Title, task.Status.ToString(), projectName);
    }

    private void RaiseCommands()
    {
        StartWorkCommand.RaiseCanExecuteChanged();
        ConfirmStartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        FinishCommand.RaiseCanExecuteChanged();
        EditProjectContextCommand.RaiseCanExecuteChanged();
        QuickCaptureCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSwitchTasks));
        OnPropertyChanged(nameof(StartWorkButtonText));
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        _heartbeatTimer.Stop();
        _idleTimer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
