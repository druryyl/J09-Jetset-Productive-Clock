using System.Collections.ObjectModel;
using System.Windows.Threading;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using Microsoft.Win32;

namespace Jetset.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly IdleAutoPauseController _idleAutoPause;
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _heartbeatTimer;
    private readonly DispatcherTimer _idleTimer;

    private string _currentTime = string.Empty;
    private string _currentDate = string.Empty;
    private string _todayTotalText = "Today: 0m";
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
    private int _inProgressCount;
    private bool _hasWaitingSessions;

    public MainWindowViewModel(AppServices services)
    {
        _services = services;
        _idleAutoPause = services.IdleAutoPause;
        Settings = services.Settings.Settings;
        StartSession = new StartSessionViewModel();
        WaitingSessions = new ObservableCollection<InProgressSessionItem>();

        StartWorkCommand = new RelayCommand(() =>
        {
            if (IsCompact)
            {
                IsCompact = false;
                _services.Settings.Update(s => s.CompactMode = false);
            }

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
        OpenTasksCommand = new RelayCommand(() => OpenTasksRequested?.Invoke(this, EventArgs.Empty));
        OpenSettingsCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        ToggleCompactCommand = new RelayCommand(ToggleCompact);
        SwitchToSessionCommand = new RelayCommand(SwitchToSession);

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
        RefreshDisplay();
    }

    public AppSettings Settings { get; private set; }

    public StartSessionViewModel StartSession { get; }

    public ObservableCollection<InProgressSessionItem> WaitingSessions { get; }

    public RelayCommand StartWorkCommand { get; }
    public RelayCommand CancelStartCommand { get; }
    public RelayCommand ConfirmStartCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand FinishCommand { get; }
    public RelayCommand OpenHistoryCommand { get; }
    public RelayCommand OpenTasksCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand ToggleCompactCommand { get; }
    public RelayCommand SwitchToSessionCommand { get; }

    public event EventHandler? OpenHistoryRequested;
    public event EventHandler? OpenTasksRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? FinishNoteRequested;
    public event EventHandler<WorkSession>? RecoveryNeeded;

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

    public int InProgressCount
    {
        get => _inProgressCount;
        private set
        {
            if (SetProperty(ref _inProgressCount, value))
            {
                OnPropertyChanged(nameof(StartWorkButtonText));
            }
        }
    }

    public bool HasWaitingSessions
    {
        get => _hasWaitingSessions;
        private set => SetProperty(ref _hasWaitingSessions, value);
    }

    public string StartWorkButtonText => InProgressCount > 0 ? "Start another" : "Start Work";

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
        _services.Sessions.FinishAtLastKnownActivity();
        _idleAutoPause.NotifySessionEnded();
        RefreshFromSession();
    }

    public void ApplyRecoveryDiscard()
    {
        _services.Sessions.Discard();
        _idleAutoPause.NotifySessionEnded();
        RefreshFromSession();
    }

    public void CompleteFinish(string? note)
    {
        _services.Sessions.Finish(note);
        _idleAutoPause.NotifySessionEnded();
        RefreshFromSession();
    }

    private void ConfirmStart()
    {
        ValidationMessage = null;
        if (!StartSession.TryBuild(out var taskName, out var mode, out var duration, out var error))
        {
            ValidationMessage = error;
            return;
        }

        try
        {
            _idleAutoPause.NotifyManualResume();
            _services.Sessions.Start(taskName, mode, duration);
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
            _services.Sessions.Pause();
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

    private void SwitchToSession(object? parameter)
    {
        Guid sessionId;
        if (parameter is Guid id)
        {
            sessionId = id;
        }
        else if (parameter is string text && Guid.TryParse(text, out var parsed))
        {
            sessionId = parsed;
        }
        else
        {
            return;
        }

        try
        {
            _idleAutoPause.NotifyManualResume();
            _services.Sessions.SwitchTo(sessionId);
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void Finish()
    {
        FinishNoteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleCompact()
    {
        IsCompact = !IsCompact;
        _services.Settings.Update(s => s.CompactMode = IsCompact);
    }

    private void RefreshFromSession()
    {
        var session = _services.Sessions.ActiveSession;
        var inProgress = _services.Sessions.GetInProgressSessions();
        InProgressCount = inProgress.Count;

        if (session is null)
        {
            _idleAutoPause.NotifySessionEnded();
            UiState = UiSessionState.Idle;
            TaskName = string.Empty;
            TimerDisplay = string.Empty;
            ModeText = string.Empty;
            StatusText = string.Empty;
            IsOvertime = false;
        }
        else
        {
            UiState = session.State == SessionState.Paused ? UiSessionState.Paused : UiSessionState.Running;
            TaskName = session.TaskName;
            ModeText = session.Mode == TimerMode.Countdown ? "Countdown" : "Stopwatch";
            StatusText = session.State == SessionState.Paused
                ? (_idleAutoPause.PausedByIdle ? "Paused (idle)" : "Paused")
                : "Running";
        }

        SyncWaitingSessions(inProgress, session?.Id);
        TodayTotalText = $"Today: {DurationFormatter.FormatFriendly(_services.Sessions.GetTodaysTotal())}";
        RaiseCommands();
        RefreshDisplay();
    }

    private void SyncWaitingSessions(IReadOnlyList<WorkSession> inProgress, Guid? focusedId)
    {
        var waiting = inProgress.Where(s => s.Id != focusedId).ToList();
        HasWaitingSessions = waiting.Count > 0;

        for (var i = WaitingSessions.Count - 1; i >= 0; i--)
        {
            if (waiting.All(s => s.Id != WaitingSessions[i].SessionId))
            {
                WaitingSessions.RemoveAt(i);
            }
        }

        foreach (var session in waiting)
        {
            var existing = WaitingSessions.FirstOrDefault(i => i.SessionId == session.Id);
            if (existing is null)
            {
                WaitingSessions.Add(new InProgressSessionItem(session, SwitchToSessionCommand));
            }
        }

        // Keep UI order stable: match store order excluding focused.
        for (var i = 0; i < waiting.Count; i++)
        {
            var currentIndex = -1;
            for (var j = 0; j < WaitingSessions.Count; j++)
            {
                if (WaitingSessions[j].SessionId == waiting[i].Id)
                {
                    currentIndex = j;
                    break;
                }
            }

            if (currentIndex >= 0 && currentIndex != i && i < WaitingSessions.Count)
            {
                WaitingSessions.Move(currentIndex, i);
            }
        }
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
            TodayTotalText = $"Today: {DurationFormatter.FormatFriendly(_services.Sessions.GetTodaysTotal())}";
            RefreshWaitingDurations(now);
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
        var countSuffix = InProgressCount > 1 ? $"  · {InProgressCount}" : string.Empty;
        CompactLine = $"{glyph} {TimerDisplay}  {TaskName}{countSuffix}";
        RefreshWaitingDurations(now);
    }

    private void RefreshWaitingDurations(DateTimeOffset now)
    {
        foreach (var item in WaitingSessions)
        {
            var duration = _services.Sessions.GetActiveDuration(item.SessionId, now);
            item.DurationText = DurationFormatter.FormatFriendly(duration);
            item.StatusText = "Waiting";
        }
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

    private void RaiseCommands()
    {
        StartWorkCommand.RaiseCanExecuteChanged();
        ConfirmStartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        FinishCommand.RaiseCanExecuteChanged();
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
