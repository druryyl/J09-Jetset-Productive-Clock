using System.Collections.ObjectModel;
using System.Windows.Threading;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using Microsoft.Win32;

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
    private int _inProgressCount;
    private bool _hasResumeQueueItems;
    private string? _activeTaskCurrentStatus;
    private string? _activeTaskLastProgress;
    private string? _activeTaskNextAction;
    private string? _activeTaskBlocker;

    public FocusViewModel(AppServices services)
    {
        _services = services;
        _idleAutoPause = services.IdleAutoPause;
        Settings = services.Settings.Settings;
        StartSession = new StartSessionViewModel(services.Tasks);
        ResumeQueueItems = new ObservableCollection<ResumeQueueItemViewModel>();

        StartWorkCommand = new RelayCommand(() =>
        {
            if (IsCompact)
            {
                ExitCompactMode();
            }

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
        ToggleCompactCommand = new RelayCommand(ToggleCompact);
        ResumeFromQueueCommand = new RelayCommand(ResumeFromQueue);

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

    public ObservableCollection<ResumeQueueItemViewModel> ResumeQueueItems { get; }

    public RelayCommand StartWorkCommand { get; }
    public RelayCommand CancelStartCommand { get; }
    public RelayCommand ConfirmStartCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand FinishCommand { get; }
    public RelayCommand OpenHistoryCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand ToggleCompactCommand { get; }
    public RelayCommand ResumeFromQueueCommand { get; }

    public event EventHandler? OpenHistoryRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler<ContextCaptureRequest>? ContextCaptureRequested;
    public event EventHandler<WorkSession>? RecoveryNeeded;
    public event EventHandler? CompactModeChanged;

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

    public bool HasResumeQueueItems
    {
        get => _hasResumeQueueItems;
        private set => SetProperty(ref _hasResumeQueueItems, value);
    }

    public bool HasActiveTaskContext =>
        !string.IsNullOrWhiteSpace(ActiveTaskCurrentStatus) ||
        !string.IsNullOrWhiteSpace(ActiveTaskLastProgress) ||
        !string.IsNullOrWhiteSpace(ActiveTaskNextAction) ||
        !string.IsNullOrWhiteSpace(ActiveTaskBlocker);

    public string? ActiveTaskCurrentStatus
    {
        get => _activeTaskCurrentStatus;
        private set
        {
            if (SetProperty(ref _activeTaskCurrentStatus, value))
            {
                OnPropertyChanged(nameof(HasActiveTaskContext));
                OnPropertyChanged(nameof(HasActiveTaskCurrentStatus));
            }
        }
    }

    public string? ActiveTaskLastProgress
    {
        get => _activeTaskLastProgress;
        private set
        {
            if (SetProperty(ref _activeTaskLastProgress, value))
            {
                OnPropertyChanged(nameof(HasActiveTaskContext));
                OnPropertyChanged(nameof(HasActiveTaskLastProgress));
            }
        }
    }

    public string? ActiveTaskNextAction
    {
        get => _activeTaskNextAction;
        private set
        {
            if (SetProperty(ref _activeTaskNextAction, value))
            {
                OnPropertyChanged(nameof(HasActiveTaskContext));
                OnPropertyChanged(nameof(HasActiveTaskNextAction));
            }
        }
    }

    public string? ActiveTaskBlocker
    {
        get => _activeTaskBlocker;
        private set
        {
            if (SetProperty(ref _activeTaskBlocker, value))
            {
                OnPropertyChanged(nameof(HasActiveTaskContext));
                OnPropertyChanged(nameof(HasActiveTaskBlocker));
            }
        }
    }

    public bool HasActiveTaskBlocker => !string.IsNullOrWhiteSpace(ActiveTaskBlocker);

    public bool HasActiveTaskCurrentStatus => !string.IsNullOrWhiteSpace(ActiveTaskCurrentStatus);

    public bool HasActiveTaskLastProgress => !string.IsNullOrWhiteSpace(ActiveTaskLastProgress);

    public bool HasActiveTaskNextAction => !string.IsNullOrWhiteSpace(ActiveTaskNextAction);

    public string StartWorkButtonText => InProgressCount > 0 ? "Start another" : "Start Work";

    public void ExitCompactMode()
    {
        if (!IsCompact)
        {
            return;
        }

        IsCompact = false;
        _services.Settings.Update(s => s.CompactMode = false);
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

    private void CompleteFinish(string? note, WorkingContext? context = null)
    {
        _services.WorkExecution.FinishWork(note, context);
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
            if (!TryPromptContextCapture(ContextCaptureReason.Switch, out var leavingContext, out _))
            {
                return;
            }

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
            _services.WorkExecution.StartWork(taskId, mode, duration, leavingContext);
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
            if (!TryPromptContextCapture(ContextCaptureReason.Pause, out var context, out _))
            {
                return;
            }

            _idleAutoPause.NotifyManualPause();
            _services.WorkExecution.PauseWork(context);
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

    private void ResumeFromQueue(object? parameter)
    {
        Guid taskId;
        if (parameter is Guid id)
        {
            taskId = id;
        }
        else if (parameter is string text && Guid.TryParse(text, out var parsed))
        {
            taskId = parsed;
        }
        else
        {
            return;
        }

        try
        {
            if (!TryPromptContextCapture(ContextCaptureReason.Switch, out var leavingContext, out _))
            {
                return;
            }

            _idleAutoPause.NotifyManualResume();
            _services.WorkExecution.ResumeWork(taskId, leavingContext);
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
            if (!TryPromptContextCapture(ContextCaptureReason.Finish, out var context, out var note))
            {
                return;
            }

            CompleteFinish(note, context);
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private bool TryPromptContextCapture(
        ContextCaptureReason reason,
        out WorkingContext? context,
        out string? sessionNote)
    {
        context = null;
        sessionNote = null;

        var task = reason == ContextCaptureReason.Finish
            ? _services.WorkExecution.GetActiveTask()
            : _services.WorkExecution.GetLeavingTask();

        if (task is null)
        {
            return true;
        }

        var request = new ContextCaptureRequest
        {
            Task = task,
            Reason = reason
        };
        ContextCaptureRequested?.Invoke(this, request);

        if (request.Result == ContextCaptureResult.Cancelled)
        {
            return false;
        }

        sessionNote = request.SessionNote;
        if (request.Result == ContextCaptureResult.Saved)
        {
            context = request.Context;
        }

        return true;
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
            ClearActiveTaskContext();
        }
        else
        {
            UiState = session.State == SessionState.Paused ? UiSessionState.Paused : UiSessionState.Running;
            TaskName = session.TaskName;
            ModeText = session.Mode == TimerMode.Countdown ? "Countdown" : "Stopwatch";
            StatusText = session.State == SessionState.Paused
                ? (_idleAutoPause.PausedByIdle ? "Paused (idle)" : "Paused")
                : "Running";
            RefreshActiveTaskContext();
        }

        SyncResumeQueue();
        TodayTotalText = FormatTodayTotalText();
        StreakText = FormatStreakText();
        RaiseCommands();
        RefreshDisplay();
    }

    private void RefreshActiveTaskContext()
    {
        var task = _services.WorkExecution.GetActiveTask();
        if (task is null)
        {
            ClearActiveTaskContext();
            return;
        }

        ActiveTaskCurrentStatus = task.CurrentStatus;
        ActiveTaskLastProgress = task.LastProgress;
        ActiveTaskNextAction = task.NextAction;
        ActiveTaskBlocker = task.Blocker;
    }

    private void ClearActiveTaskContext()
    {
        ActiveTaskCurrentStatus = null;
        ActiveTaskLastProgress = null;
        ActiveTaskNextAction = null;
        ActiveTaskBlocker = null;
    }

    private void SyncResumeQueue()
    {
        var queue = _services.ResumeQueue.GetOrderedTasks();
        HasResumeQueueItems = queue.Count > 0;

        for (var i = ResumeQueueItems.Count - 1; i >= 0; i--)
        {
            if (queue.All(e => e.Task.Id != ResumeQueueItems[i].TaskId))
            {
                ResumeQueueItems.RemoveAt(i);
            }
        }

        foreach (var entry in queue)
        {
            var existing = ResumeQueueItems.FirstOrDefault(i => i.TaskId == entry.Task.Id);
            if (existing is null)
            {
                ResumeQueueItems.Add(new ResumeQueueItemViewModel(
                    entry.Task.Id,
                    entry.PausedSession?.Id,
                    entry.Task.Title,
                    entry.Task.CurrentStatus,
                    entry.Task.LastProgress,
                    entry.Task.NextAction,
                    entry.Task.Blocker,
                    ResumeFromQueueCommand));
            }
            else
            {
                existing.CurrentStatus = entry.Task.CurrentStatus;
                existing.LastProgress = entry.Task.LastProgress;
                existing.NextAction = entry.Task.NextAction;
                existing.Blocker = entry.Task.Blocker;
            }
        }

        for (var i = 0; i < queue.Count; i++)
        {
            var currentIndex = -1;
            for (var j = 0; j < ResumeQueueItems.Count; j++)
            {
                if (ResumeQueueItems[j].TaskId == queue[i].Task.Id)
                {
                    currentIndex = j;
                    break;
                }
            }

            if (currentIndex >= 0 && currentIndex != i && i < ResumeQueueItems.Count)
            {
                ResumeQueueItems.Move(currentIndex, i);
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
            TodayTotalText = FormatTodayTotalText();
            StreakText = FormatStreakText();
            RefreshResumeQueueDurations(now);
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
        RefreshResumeQueueDurations(now);
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

    private void RefreshResumeQueueDurations(DateTimeOffset now)
    {
        var queue = _services.ResumeQueue.GetOrderedTasks();
        foreach (var item in ResumeQueueItems)
        {
            var entry = queue.FirstOrDefault(e => e.Task.Id == item.TaskId);
            if (entry?.PausedSession is { } session)
            {
                var duration = _services.Sessions.GetActiveDuration(session.Id, now);
                item.DurationText = DurationFormatter.FormatFriendly(duration);
                item.StatusText = "Waiting";
            }
            else
            {
                item.DurationText = string.Empty;
                item.StatusText = "Ready";
            }
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
