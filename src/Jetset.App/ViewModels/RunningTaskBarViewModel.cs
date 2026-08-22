using System.Windows.Threading;
using Jetset.App.Helpers;
using Jetset.App.Models;
using Jetset.App.Services;
using Microsoft.Win32;
using TaskStatus = Jetset.App.Models.TaskStatus;

namespace Jetset.App.ViewModels;

/// <summary>
/// Execution chrome for the Work Tree workspace — running task, timer, and Done/Waiting/Pause.
/// </summary>
public sealed class RunningTaskBarViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly IdleAutoPauseController _idleAutoPause;
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _heartbeatTimer;
    private readonly DispatcherTimer _idleTimer;
    private readonly EventHandler _sessionChangedHandler;
    private readonly EventHandler _idleStateChangedHandler;

    private string _taskTitle = string.Empty;
    private string _statusText = string.Empty;
    private string _timerDisplay = string.Empty;
    private bool _isOvertime;

    public RunningTaskBarViewModel(AppServices services)
    {
        _services = services;
        _idleAutoPause = services.IdleAutoPause;

        PauseCommand = new RelayCommand(Pause, () => HasRunningTask && !IsPaused);
        ResumeCommand = new RelayCommand(Resume, () => HasRunningTask && IsPaused);
        MarkDoneCommand = new RelayCommand(MarkDone, () => HasRunningTask);
        MarkWaitingCommand = new RelayCommand(MarkWaiting, () => HasRunningTask);

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _uiTimer.Tick += (_, _) => RefreshTimerDisplay();
        _uiTimer.Start();

        _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _heartbeatTimer.Tick += (_, _) => _services.Sessions.Heartbeat();
        _heartbeatTimer.Start();

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _idleTimer.Tick += (_, _) => _idleAutoPause.Evaluate();
        _idleTimer.Start();

        _sessionChangedHandler = (_, _) => RefreshFromSession();
        _services.Sessions.SessionChanged += _sessionChangedHandler;

        _idleStateChangedHandler = (_, _) =>
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
        _idleAutoPause.StateChanged += _idleStateChangedHandler;

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        RefreshFromSession();
    }

    public event EventHandler? WorkStateChanged;

    public RelayCommand PauseCommand { get; }

    public RelayCommand ResumeCommand { get; }

    public RelayCommand MarkDoneCommand { get; }

    public RelayCommand MarkWaitingCommand { get; }

    public bool HasRunningTask { get; private set; }

    public bool IsPaused { get; private set; }

    public bool IsOvertime
    {
        get => _isOvertime;
        private set => SetProperty(ref _isOvertime, value);
    }

    public string TaskTitle
    {
        get => _taskTitle;
        private set => SetProperty(ref _taskTitle, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TimerDisplay
    {
        get => _timerDisplay;
        private set => SetProperty(ref _timerDisplay, value);
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        _heartbeatTimer.Stop();
        _idleTimer.Stop();
        _services.Sessions.SessionChanged -= _sessionChangedHandler;
        _idleAutoPause.StateChanged -= _idleStateChangedHandler;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    private void RefreshFromSession()
    {
        var runningTask = _services.Tasks.GetRunningTask();
        var session = _services.Sessions.ActiveSession;

        HasRunningTask = runningTask is not null;
        IsPaused = session?.State == SessionState.Paused;

        if (runningTask is null)
        {
            _idleAutoPause.NotifySessionEnded();
            TaskTitle = string.Empty;
            StatusText = string.Empty;
            TimerDisplay = string.Empty;
            IsOvertime = false;
        }
        else
        {
            TaskTitle = runningTask.Title;
            StatusText = session?.State == SessionState.Paused
                ? _idleAutoPause.PausedByIdle ? "Paused (idle)" : "Paused"
                : "Running";
            RefreshTimerDisplay();
        }

        OnPropertyChanged(nameof(HasRunningTask));
        OnPropertyChanged(nameof(IsPaused));
        RaiseCommands();
        WorkStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshTimerDisplay()
    {
        var session = _services.Sessions.ActiveSession;
        if (session is null)
        {
            TimerDisplay = string.Empty;
            IsOvertime = false;
            return;
        }

        var now = _services.Clock.Now;
        var active = _services.Sessions.GetActiveDuration(session.Id, now);

        if (session.Mode == TimerMode.Stopwatch)
        {
            TimerDisplay = DurationFormatter.FormatTimer(active);
            IsOvertime = false;
            return;
        }

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

    private void MaybeNotifyCountdownComplete(WorkSession session)
    {
        if (session.CountdownCompletedNotified || session.State != SessionState.Running)
        {
            return;
        }

        _services.Sessions.MarkCountdownNotified();
        _services.Notifications.ShowBalloon("Jetset", $"Countdown finished: {session.TaskName}");
        _services.Notifications.ShowCountdownCompleted(
            session.TaskName,
            _services.Settings.Settings.SoundOnCountdownComplete);
        StatusText = "Countdown complete — overtime";
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
            StatusText = ex.Message;
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
            StatusText = ex.Message;
        }
    }

    private void MarkDone()
    {
        var runningTask = _services.Tasks.GetRunningTask();
        if (runningTask is null)
        {
            return;
        }

        try
        {
            _services.WorkExecution.FinishWork();
            _idleAutoPause.NotifySessionEnded();
            _services.Tasks.CompleteTask(runningTask.Id);
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    private void MarkWaiting()
    {
        var runningTask = _services.Tasks.GetRunningTask();
        if (runningTask is null)
        {
            return;
        }

        var taskId = runningTask.Id;

        try
        {
            _services.WorkExecution.FinishWork();
            _idleAutoPause.NotifySessionEnded();
            _services.Tasks.ChangeStatus(taskId, TaskStatus.Waiting);
            RefreshFromSession();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
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
                RefreshTimerDisplay();
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
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        MarkDoneCommand.RaiseCanExecuteChanged();
        MarkWaitingCommand.RaiseCanExecuteChanged();
    }
}
